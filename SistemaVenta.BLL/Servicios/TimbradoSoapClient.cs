using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SistemaVenta.BLL.Servicios
{
    public class TimbradoSoapClient
    {
        //  CONFIGURACIÓN CORRECTA SEGÚN WSDL DE URBANSA
        private readonly string _url = "https://ws.urbansa.com/app/timbrado.asmx";
        private readonly string _namespace = "http://ws.urbansa.com/";
        private readonly string _soapAction = "http://ws.urbansa.com/TimbrarF";

        public async Task<byte[]> TimbrarF(string usuario, string password, string xmlComprobante)
        {
            HttpClient httpClient = null;

            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromMinutes(5)
                };

                Console.WriteLine($"========================================");
                Console.WriteLine($"[TIMBRADO] Iniciando timbrado...");
                Console.WriteLine($"[TIMBRADO] URL: {_url}");
                Console.WriteLine($"[TIMBRADO] Namespace: {_namespace}");
                Console.WriteLine($"[TIMBRADO] SOAPAction: {_soapAction}");
                Console.WriteLine($"[TIMBRADO] Usuario: {usuario}");
                Console.WriteLine($"========================================");

                // Construir SOAP envelope
                string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <TimbrarF xmlns=""{_namespace}"">
            <Usuario>{EscapeXml(usuario)}</Usuario>
            <Password>{EscapeXml(password)}</Password>
            <StrXml>{EscapeXml(xmlComprobante)}</StrXml>
        </TimbrarF>
    </soap:Body>
</soap:Envelope>";

                Console.WriteLine($"[TIMBRADO] SOAP construido: {soapEnvelope.Length} chars");
                Console.WriteLine($"[TIMBRADO] XML Comprobante preview (500 chars):");
                Console.WriteLine(xmlComprobante.Substring(0, Math.Min(500, xmlComprobante.Length)));

                // Crear petición HTTP
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
                content.Headers.Add("SOAPAction", $"\"{_soapAction}\"");

                Console.WriteLine($"[TIMBRADO] Enviando petición...");

                // Enviar petición
                var response = await httpClient.PostAsync(_url, content);

                Console.WriteLine($"[TIMBRADO] Status: {response.StatusCode}");

                // Leer respuesta
                string responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[TIMBRADO] Respuesta: {responseContent.Length} chars");

                // Verificar errores HTTP
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[TIMBRADO]  ERROR HTTP {response.StatusCode}");
                    Console.WriteLine($"[TIMBRADO] Respuesta:");
                    Console.WriteLine(responseContent);

                    //  Parsear el error aunque sea HTTP 500, porque puede contener SOAP Fault
                }

                // Parsear respuesta SOAP
                XDocument doc = XDocument.Parse(responseContent);
                XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
                XNamespace ns = _namespace;

                // Verificar SOAP Fault
                var faultElement = doc.Descendants(soap + "Fault").FirstOrDefault();
                if (faultElement != null)
                {
                    string faultCode = faultElement.Element("faultcode")?.Value ?? "";
                    string faultString = faultElement.Element("faultstring")?.Value ?? "Error desconocido";

                    // Extraer información del detalle
                    var detailElement = faultElement.Element("detail");
                    string errorNumber = "";
                    string errorMessage = "";

                    if (detailElement != null)
                    {
                        XNamespace errorNs = "Timbrar";
                        var errorElement = detailElement.Descendants(errorNs + "Error").FirstOrDefault();

                        if (errorElement != null)
                        {
                            errorNumber = errorElement.Element(errorNs + "ErrorNumber")?.Value ?? "";
                            errorMessage = errorElement.Element(errorNs + "ErrorMessage")?.Value ?? "";
                        }
                    }

                    Console.WriteLine($"[TIMBRADO]  SOAP Fault:");
                    Console.WriteLine($"[TIMBRADO]    Code: {faultCode}");
                    Console.WriteLine($"[TIMBRADO]    String: {faultString}");
                    Console.WriteLine($"[TIMBRADO]    Error Number: {errorNumber}");
                    Console.WriteLine($"[TIMBRADO]    Error Message: {errorMessage}");

                    //  Manejar error 1251 (folio duplicado)
                    if (errorNumber == "1251")
                    {
                        throw new FolioDuplicadoException(
                            $"Este folio ya fue timbrado anteriormente. " +
                            $"No se puede timbrar el mismo folio dos veces. " +
                            $"Detalle: {errorMessage}"
                        );
                    }

                    throw new SoapException($"{faultString}\nError #{errorNumber}: {errorMessage}");
                }

                // Si llegamos aquí y hubo error HTTP, lanzar excepción
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Error HTTP {response.StatusCode}: {response.ReasonPhrase}\n" +
                        $"Respuesta: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}"
                    );
                }

                // Extraer resultado (base64)
                var resultElement = doc.Descendants(ns + "TimbrarFResult").FirstOrDefault();

                if (resultElement == null)
                {
                    Console.WriteLine($"[TIMBRADO] Buscando sin namespace...");
                    resultElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "TimbrarFResult");
                }

                if (resultElement == null || string.IsNullOrWhiteSpace(resultElement.Value))
                {
                    Console.WriteLine($"[TIMBRADO]  No se encontró TimbrarFResult");
                    Console.WriteLine($"[TIMBRADO] Respuesta XML:");
                    Console.WriteLine(responseContent);
                    throw new Exception("El servicio no devolvió TimbrarFResult");
                }

                Console.WriteLine($"[TIMBRADO]  TimbrarFResult encontrado: {resultElement.Value.Length} chars");

                // Decodificar Base64
                byte[] zipBytes = Convert.FromBase64String(resultElement.Value);

                Console.WriteLine($"[TIMBRADO]  ZIP decodificado: {zipBytes.Length} bytes");
                Console.WriteLine($"[TIMBRADO]  TIMBRADO EXITOSO ");
                Console.WriteLine($"========================================");

                return zipBytes;
            }
            catch (FolioDuplicadoException)
            {
                throw; // Re-lanzar sin modificar
            }
            catch (SoapException)
            {
                throw; // Re-lanzar sin modificar
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"[TIMBRADO]  HTTP Exception: {httpEx.Message}");
                throw new Exception($"Error de conexión: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[TIMBRADO]  Timeout");
                throw new Exception("El servicio tardó demasiado en responder");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TIMBRADO]  Exception: {ex.Message}");
                Console.WriteLine($"[TIMBRADO] Stack: {ex.StackTrace}");
                throw new Exception($"Error al llamar al servicio de timbrado: {ex.Message}", ex);
            }
            finally
            {
                httpClient?.Dispose();
            }
        }

        private string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }

    //  EXCEPCIONES PERSONALIZADAS
    public class SoapException : Exception
    {
        public SoapException(string message) : base(message) { }
        public SoapException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class FolioDuplicadoException : Exception
    {
        public FolioDuplicadoException(string message) : base(message) { }
        public FolioDuplicadoException(string message, Exception innerException) : base(message, innerException) { }
    }
}
