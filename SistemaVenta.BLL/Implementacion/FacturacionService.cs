using SistemaVenta.BLL.Interfaces;
using SistemaVenta.BLL.Servicios;
using SistemaVenta.DAL.Interfaces;
using SistemaVenta.Entity.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SistemaVenta.BLL.Implementacion
{
    public class FacturacionService : IFacturacionService
    {
        private readonly INegocioService _negocioService;
        private readonly IGenericRepository<Producto> _repositorioProducto; //  AGREGAR

        public FacturacionService(
            INegocioService negocioService,
            IGenericRepository<Producto> repositorioProducto) //  AGREGAR
        {
            _negocioService = negocioService;
            _repositorioProducto = repositorioProducto; //  AGREGAR
        }

        public bool ValidarDatosFiscalesCliente(Cliente cliente)
        {
            if (cliente == null) return false;

            return !string.IsNullOrWhiteSpace(cliente.Rfc) &&
                   !string.IsNullOrWhiteSpace(cliente.DomicilioFiscalReceptor) &&
                   !string.IsNullOrWhiteSpace(cliente.RegimenFiscalReceptor) &&
                   !string.IsNullOrWhiteSpace(cliente.Nombre);
        }

        public async Task<bool> ValidarConfiguracionNegocio()
        {
            try
            {
                var negocio = await _negocioService.Obtener();

                if (negocio == null)
                    return false;

                bool datosBasicos = !string.IsNullOrWhiteSpace(negocio.Rfc) &&
                                   !string.IsNullOrWhiteSpace(negocio.Nombre) &&
                                   !string.IsNullOrWhiteSpace(negocio.RegimenFiscal) &&
                                   !string.IsNullOrWhiteSpace(negocio.CodigoPostal);

                bool credencialesPAC = !string.IsNullOrWhiteSpace(negocio.UsuarioPac) &&
                                       !string.IsNullOrWhiteSpace(negocio.PasswordPac);

                return datosBasicos && credencialesPAC;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool exito, string rutaPDF, string rutaXML, string uuid, string mensaje)> TimbrarVenta(
            Venta venta,
            Cliente cliente,
            string rutaGuardado)
        {
            try
            {
                // 1. Obtener datos del negocio
                Negocio negocio = await _negocioService.Obtener();

                if (negocio == null)
                {
                    return (false, null, null, null, "No se encontró la configuración del negocio");
                }

                // 2. Validar datos del negocio
                if (string.IsNullOrWhiteSpace(negocio.Rfc) ||
                    string.IsNullOrWhiteSpace(negocio.Nombre) ||
                    string.IsNullOrWhiteSpace(negocio.RegimenFiscal) ||
                    string.IsNullOrWhiteSpace(negocio.CodigoPostal))
                {
                    return (false, null, null, null,
                        "El negocio no tiene configurados todos los datos fiscales requeridos");
                }

                // 3. Validar datos fiscales del cliente
                if (!ValidarDatosFiscalesCliente(cliente))
                {
                    return (false, null, null, null, "El cliente no tiene datos fiscales completos");
                }

                // 4. Obtener credenciales del PAC
                string usuarioPAC = negocio.UsuarioPac;
                string passwordPAC = negocio.PasswordPac;

                if (string.IsNullOrWhiteSpace(usuarioPAC) || string.IsNullOrWhiteSpace(passwordPAC))
                {
                    return (false, null, null, null, "No se han configurado las credenciales del PAC");
                }

                // 5. Construir XML
                string xmlComprobante = await ConstruirXMLComprobante(venta, cliente, negocio); //  CAMBIAR A ASYNC

                // 6. Llamar al servicio de timbrado
                TimbradoSoapClient timbradoService = new TimbradoSoapClient();
                byte[] zipBytes = await timbradoService.TimbrarF(usuarioPAC, passwordPAC, xmlComprobante);

                if (zipBytes == null || zipBytes.Length == 0)
                {
                    return (false, null, null, null, "El servicio de timbrado no devolvió datos");
                }

                // 7. Extraer archivos
                var (pdfBytes, xmlBytes, uuid) = ExtraerArchivosDelZIP(zipBytes);

                if (pdfBytes == null || xmlBytes == null)
                {
                    return (false, null, null, null, "No se pudieron extraer los archivos del ZIP");
                }

                // 8. Crear directorio
                if (!Directory.Exists(rutaGuardado))
                {
                    Directory.CreateDirectory(rutaGuardado);
                }

                // 9. Guardar archivos
                string nombreArchivo = $"Factura_{venta.NumeroVenta}_{DateTime.Now:yyyyMMddHHmmss}";
                string rutaPDF = Path.Combine(rutaGuardado, $"{nombreArchivo}.pdf");
                string rutaXML = Path.Combine(rutaGuardado, $"{nombreArchivo}.xml");

                await File.WriteAllBytesAsync(rutaPDF, pdfBytes);
                await File.WriteAllBytesAsync(rutaXML, xmlBytes);

                return (true, rutaPDF, rutaXML, uuid, "Factura timbrada correctamente");
            }
            catch (FolioDuplicadoException folioEx)
            {
                return (false, null, null, null,
                    " Este folio ya fue timbrado anteriormente. " +
                    "La factura ya existe en el SAT. " +
                    "No es necesario volver a timbrarla.");
            }
            catch (SoapException soapEx)
            {
                return (false, null, null, null, $"Error del PAC: {soapEx.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, null, null, $"Error inesperado: {ex.Message}");
            }
        }

        //  MÉTODO CORREGIDO CON DESCUENTOS
        private async Task<string> ConstruirXMLComprobante(Venta venta, Cliente cliente, Negocio negocio)
        {
            Console.WriteLine($"========================================");
            Console.WriteLine($"[DEBUG CLIENTE] Nombre: {cliente.Nombre}");
            Console.WriteLine($"[DEBUG CLIENTE] RFC: '{cliente.Rfc}'");
            Console.WriteLine($"[DEBUG CLIENTE] Régimen: '{cliente.RegimenFiscalReceptor}'");
            Console.WriteLine($"========================================");

            StringBuilder xml = new StringBuilder();

            // Calcular totales con descuento
            decimal totalDescuentos = 0;
            decimal subtotalSinDescuento = 0;
            decimal totalImpuestosTrasladados = 0;
            decimal totalImpuestosRetenidos = 0;

            //  PRIMER FOREACH - CALCULAR TOTALES (CORREGIDO)
            foreach (var detalle in venta.DetalleVenta)
            {
                var producto = await _repositorioProducto.Obtener(p => p.IdProducto == detalle.IdProducto);

                decimal precioOriginal = producto?.Precio ?? detalle.Precio ?? 0;
                decimal porcentajeDescuento = producto?.Descuento ?? 0;
                int cantidad = detalle.Cantidad ?? 0;

                //  CORREGIDO - CONVERTIR DE STRING A INT
                string tipoImpuestoStr = producto?.TipoImpuesto ?? "1";
                int tipoImpuesto = int.TryParse(tipoImpuestoStr, out int tipo) ? tipo : 1;

                decimal valorImpuesto = producto?.ValorImpuesto ?? 0.16m;

                decimal importeSinDescuento = precioOriginal * cantidad;
                decimal montoDescuento = importeSinDescuento * (porcentajeDescuento / 100);
                decimal importeConDescuento = importeSinDescuento - montoDescuento;
                decimal impuestoConcepto = importeConDescuento * valorImpuesto;

                subtotalSinDescuento += importeSinDescuento;
                totalDescuentos += montoDescuento;

                //  Acumular según tipo de impuesto
                if (tipoImpuesto == 1) // Trasladado
                    totalImpuestosTrasladados += impuestoConcepto;
                else if (tipoImpuesto == 2) // Retenido
                    totalImpuestosRetenidos += impuestoConcepto;
            }

            decimal subtotalConDescuento = subtotalSinDescuento - totalDescuentos;
            decimal totalFinal = subtotalConDescuento + totalImpuestosTrasladados - totalImpuestosRetenidos;

            //  INICIO DEL XML - DATOS DEL COMPROBANTE
            xml.AppendLine("<Comprobante>");
            xml.AppendLine($"    <idLocal>{venta.NumeroVenta}</idLocal>");
            xml.AppendLine("    <version>4.0</version>");
            xml.AppendLine($"    <serie>{negocio.SerieFactura ?? ""}</serie>");
            xml.AppendLine($"    <folio>{venta.NumeroVenta}</folio>");
            xml.AppendLine($"    <fecha>{venta.FechaRegistro:yyyy-MM-ddTHH:mm:ss}</fecha>");
            xml.AppendLine("    <formaPago>01</formaPago>");
            xml.AppendLine("    <condicionesDePago>CONTADO</condicionesDePago>");
            xml.AppendLine($"    <subTotal>{subtotalSinDescuento:F2}</subTotal>");
            xml.AppendLine($"    <descuento>{totalDescuentos:F2}</descuento>");
            xml.AppendLine($"    <moneda>{negocio.SimboloMoneda ?? "MXN"}</moneda>");
            xml.AppendLine("    <tipoCambio>1</tipoCambio>");
            xml.AppendLine($"    <total>{totalFinal:F2}</total>");
            xml.AppendLine("    <tipoDeComprobante>I</tipoDeComprobante>");
            xml.AppendLine("    <exportacion>01</exportacion>");
            xml.AppendLine("    <metodoPago>PUE</metodoPago>");
            xml.AppendLine($"    <lugarExpedicion>{negocio.CodigoPostal}</lugarExpedicion>");
            xml.AppendLine("    <confirmacion></confirmacion>");
            xml.AppendLine("    <Relacionado/>");

            //  DATOS DEL EMISOR (NEGOCIO)
            xml.AppendLine($"    <regimenFiscal>{negocio.RegimenFiscal}</regimenFiscal>");
            xml.AppendLine($"    <rfc>{negocio.Rfc}</rfc>");
            xml.AppendLine($"    <nombre>{negocio.Nombre}</nombre>");
            xml.AppendLine("    <residenciaFiscal></residenciaFiscal>");
            xml.AppendLine("    <numRegIdTrib></numRegIdTrib>");

            //  DATOS DEL RECEPTOR (CLIENTE)
            xml.AppendLine("    <usoCFDI>S01</usoCFDI>");
            xml.AppendLine($"    <domicilioFiscalReceptor>{cliente.DomicilioFiscalReceptor}</domicilioFiscalReceptor>");
            xml.AppendLine($"    <rfcReceptor>{cliente.Rfc}</rfcReceptor>");
            xml.AppendLine($"    <nombreReceptor>{cliente.Nombre}</nombreReceptor>");
            xml.AppendLine($"    <regimenFiscalReceptor>{cliente.RegimenFiscalReceptor}</regimenFiscalReceptor>");
            xml.AppendLine($"    <email>{cliente.Correo ?? ""}</email>");

            //  SEGUNDO FOREACH - GENERAR CONCEPTOS (YA ESTABA CORREGIDO)
            foreach (var detalle in venta.DetalleVenta)
            {
                var producto = await _repositorioProducto.Obtener(p => p.IdProducto == detalle.IdProducto);

                //  OBTENER DATOS DEL PRODUCTO
                string claveProdServ = producto?.ClaveProductoSat ?? "01010101";
                string claveUnidad = producto?.MedidaSat ?? "H87";
                string unidad = producto?.MedidaEmpresa ?? "Pieza";
                string objetoImp = producto?.ObjetoImpuesto ?? "02";
                string tipoImpuestoStr = producto?.TipoImpuesto ?? "1";
                string factorImpuesto = producto?.FactorImpuesto ?? "Tasa";
                string impuesto = producto?.Impuesto ?? "002";
                decimal valorImpuesto = producto?.ValorImpuesto ?? 0.16m;

                //  CONVERTIR A INT
                int tipoImpuesto = int.TryParse(tipoImpuestoStr, out int tipo) ? tipo : 1;

                decimal precioOriginal = producto?.Precio ?? detalle.Precio ?? 0;
                decimal porcentajeDescuento = producto?.Descuento ?? 0;
                int cantidad = detalle.Cantidad ?? 0;

                decimal importeSinDescuento = precioOriginal * cantidad;
                decimal montoDescuento = importeSinDescuento * (porcentajeDescuento / 100);
                decimal importeConDescuento = importeSinDescuento - montoDescuento;
                decimal baseImpuesto = importeConDescuento;
                decimal impuestoConcepto = baseImpuesto * valorImpuesto;

                Console.WriteLine($"[CONCEPTO] {detalle.DescripcionProducto}");
                Console.WriteLine($"[CONCEPTO] Tipo: {(tipoImpuesto == 1 ? "Trasladado" : "Retenido")} | Factor: {factorImpuesto} | Valor: {valorImpuesto:F4}");

                xml.AppendLine("    <Concepto>");
                xml.AppendLine($"        <claveProdServ>{claveProdServ}</claveProdServ>");
                xml.AppendLine($"        <noIdentificacion>{detalle.IdProducto}</noIdentificacion>");
                xml.AppendLine($"        <cantidad>{cantidad}</cantidad>");
                xml.AppendLine($"        <claveUnidad>{claveUnidad}</claveUnidad>");
                xml.AppendLine($"        <unidad>{unidad}</unidad>");
                xml.AppendLine($"        <descripcion>{detalle.DescripcionProducto}</descripcion>");
                xml.AppendLine($"        <valorUnitario>{precioOriginal:F2}</valorUnitario>");
                xml.AppendLine($"        <importe>{importeSinDescuento:F2}</importe>");
                xml.AppendLine($"        <descuento>{montoDescuento:F2}</descuento>");
                xml.AppendLine("        <cuentaPredial></cuentaPredial>");
                xml.AppendLine($"        <objetoImp>{objetoImp}</objetoImp>");

                xml.AppendLine("        <Impuestos>");

                if (tipoImpuesto == 1) // Trasladado
                {
                    xml.AppendLine("            <Traslados>");
                    xml.AppendLine("                <Traslado>");
                    xml.AppendLine($"                    <base>{baseImpuesto:F2}</base>");
                    xml.AppendLine($"                    <impuesto>{impuesto}</impuesto>");
                    xml.AppendLine($"                    <tipoFactor>{factorImpuesto}</tipoFactor>");
                    xml.AppendLine($"                    <tasaOCuota>{valorImpuesto:F6}</tasaOCuota>");
                    xml.AppendLine($"                    <importe>{impuestoConcepto:F2}</importe>");
                    xml.AppendLine("                </Traslado>");
                    xml.AppendLine("            </Traslados>");
                }
                else if (tipoImpuesto == 2) // Retenido
                {
                    xml.AppendLine("            <Retenciones>");
                    xml.AppendLine("                <Retencion>");
                    xml.AppendLine($"                    <base>{baseImpuesto:F2}</base>");
                    xml.AppendLine($"                    <impuesto>{impuesto}</impuesto>");
                    xml.AppendLine($"                    <tipoFactor>{factorImpuesto}</tipoFactor>");
                    xml.AppendLine($"                    <tasaOCuota>{valorImpuesto:F6}</tasaOCuota>");
                    xml.AppendLine($"                    <importe>{impuestoConcepto:F2}</importe>");
                    xml.AppendLine("                </Retencion>");
                    xml.AppendLine("            </Retenciones>");
                }

                xml.AppendLine("        </Impuestos>");
                xml.AppendLine("    </Concepto>");
            }

            //  IMPUESTOS TOTALES
            xml.AppendLine("    <Impuestos>");

            if (totalImpuestosTrasladados > 0)
                xml.AppendLine($"    <totalImpuestosTrasladados>{totalImpuestosTrasladados:F2}</totalImpuestosTrasladados>");

            if (totalImpuestosRetenidos > 0)
                xml.AppendLine($"    <totalImpuestosRetenidos>{totalImpuestosRetenidos:F2}</totalImpuestosRetenidos>");

            if (totalImpuestosTrasladados > 0)
            {
                xml.AppendLine("        <Traslados>");
                xml.AppendLine("            <Traslado>");
                xml.AppendLine($"                <base>{subtotalConDescuento:F2}</base>");
                xml.AppendLine("                <impuesto>002</impuesto>");
                xml.AppendLine("                <tipoFactor>Tasa</tipoFactor>");
                xml.AppendLine("                <tasaOCuota>0.160000</tasaOCuota>");
                xml.AppendLine($"                <importe>{totalImpuestosTrasladados:F2}</importe>");
                xml.AppendLine("            </Traslado>");
                xml.AppendLine("        </Traslados>");
            }

            if (totalImpuestosRetenidos > 0)
            {
                xml.AppendLine("        <Retenciones>");
                xml.AppendLine("            <Retencion>");
                xml.AppendLine($"                <base>{subtotalConDescuento:F2}</base>");
                xml.AppendLine("                <impuesto>002</impuesto>");
                xml.AppendLine("                <tipoFactor>Tasa</tipoFactor>");
                xml.AppendLine("                <tasaOCuota>0.160000</tasaOCuota>");
                xml.AppendLine($"                <importe>{totalImpuestosRetenidos:F2}</importe>");
                xml.AppendLine("            </Retencion>");
                xml.AppendLine("        </Retenciones>");
            }

            xml.AppendLine("    </Impuestos>");
            xml.AppendLine("</Comprobante>");

            string xmlCompleto = xml.ToString();
            Console.WriteLine($"[XML COMPLETO] Primeros 2000 caracteres:");
            Console.WriteLine(xmlCompleto.Substring(0, Math.Min(2000, xmlCompleto.Length)));
            Console.WriteLine($"========================================");

            return xmlCompleto;
        }





        private (byte[] pdf, byte[] xml, string uuid) ExtraerArchivosDelZIP(byte[] zipBytes)
        {
            byte[] pdfBytes = null;
            byte[] xmlBytes = null;
            string uuid = null;

            using (MemoryStream zipStream = new MemoryStream(zipBytes))
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var pdfEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

                if (pdfEntry != null)
                {
                    using (Stream pdfStream = pdfEntry.Open())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        pdfStream.CopyTo(ms);
                        pdfBytes = ms.ToArray();
                    }
                }

                var xmlEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                if (xmlEntry != null)
                {
                    using (Stream xmlStream = xmlEntry.Open())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        xmlStream.CopyTo(ms);
                        xmlBytes = ms.ToArray();

                        try
                        {
                            ms.Position = 0;
                            XDocument doc = XDocument.Load(ms);
                            var timbreFiscal = doc.Descendants()
                                .FirstOrDefault(e => e.Name.LocalName == "TimbreFiscalDigital");

                            uuid = timbreFiscal?.Attribute("UUID")?.Value;
                        }
                        catch { }
                    }
                }
            }

            return (pdfBytes, xmlBytes, uuid);
        }
    }
}
