using AutoMapper;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaVenta.AplicacionWeb.Models.ViewModels;
using SistemaVenta.AplicacionWeb.Utilidades.Response;
using SistemaVenta.BLL.Interfaces;
using SistemaVenta.Entity;
using SistemaVenta.Entity.Models;
using System.Security.Claims;

namespace SistemaVenta.AplicacionWeb.Controllers
{
    [Authorize]
    public class VentaController : Controller
    {
        private readonly ITipoDocumentoVentaService _tipoDocumentoVentaServicio;
        private readonly IVentaService _ventaServicio;
        private readonly IClienteService _clienteServicio;
        private readonly IFacturacionService _facturacionServicio;
        private readonly IMapper _mapper;
        private readonly IConverter _converter;
        private readonly IWebHostEnvironment _env;

        public VentaController(
            ITipoDocumentoVentaService tipoDocumentoVentaServicio,
            IVentaService ventaServicio,
            IClienteService clienteServicio,
            IFacturacionService facturacionServicio,
            IMapper mapper,
            IConverter converter,
            IWebHostEnvironment env
            )
        {
            _tipoDocumentoVentaServicio = tipoDocumentoVentaServicio;
            _ventaServicio = ventaServicio;
            _clienteServicio = clienteServicio;
            _facturacionServicio = facturacionServicio;
            _mapper = mapper;
            _converter = converter;
            _env = env;
        }

        public IActionResult NuevaVenta()
        {
            return View();
        }

        public IActionResult HistorialVenta()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ListaTipoDocumentoVenta()
        {
            List<VMTipoDocumentoVenta> vmListaTipoDocumentos = _mapper.Map<List<VMTipoDocumentoVenta>>(
                await _tipoDocumentoVentaServicio.Lista()
            );
            return StatusCode(StatusCodes.Status200OK, vmListaTipoDocumentos);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductos(string busqueda)
        {
            List<VMProducto> vmListaProductos = _mapper.Map<List<VMProducto>>(
                await _ventaServicio.ObtenerProductos(busqueda)
            );
            return StatusCode(StatusCodes.Status200OK, vmListaProductos);
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarVenta([FromBody] VMVenta modelo)
        {
            GenericResponse<VMVenta> gResponse = new GenericResponse<VMVenta>();

            try
            {
                ClaimsPrincipal claimUser = HttpContext.User;

                string idUsuario = claimUser.Claims
                    .Where(c => c.Type == ClaimTypes.NameIdentifier)
                    .Select(c => c.Value).SingleOrDefault();

                modelo.IdUsuario = int.Parse(idUsuario);

                //  SECCIÓN DE FACTURACIÓN MODIFICADA
                if (modelo.IdTipoDocumentoVenta == 2) // Factura
                {
                    // Validar que tenga cliente
                    if (!modelo.IdCliente.HasValue || modelo.IdCliente.Value == 0)
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "Debe seleccionar un cliente para generar factura";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    //  ERROR 1 CORREGIDO: Obtener cliente usando expresión lambda
                    Cliente cliente = await _clienteServicio.Obtener(c => c.IdCliente == modelo.IdCliente.Value);

                    if (cliente == null)
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "Cliente no encontrado";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    // Validar datos fiscales del cliente
                    if (!_facturacionServicio.ValidarDatosFiscalesCliente(cliente))
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "El cliente no tiene datos fiscales completos (RFC, Domicilio Fiscal, Régimen Fiscal)";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    // Validar configuración del negocio
                    bool configuracionValida = await _facturacionServicio.ValidarConfiguracionNegocio();
                    if (!configuracionValida)
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "La configuración del negocio para facturación no está completa";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    // Registrar venta
                    Venta venta_creada = await _ventaServicio.Registrar(_mapper.Map<Venta>(modelo));

                    if (venta_creada.IdVenta == 0)
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "No se pudo registrar la venta";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    // Timbrar factura
                    string rutaFacturas = Path.Combine(_env.WebRootPath, "Facturas");

                    var resultado = await _facturacionServicio.TimbrarVenta(venta_creada, cliente, rutaFacturas);

                    if (!resultado.exito)
                    {
                        //  Si es error de folio duplicado, considerar la venta como exitosa
                        if (resultado.mensaje.Contains("ya fue timbrado") || resultado.mensaje.Contains("1251"))
                        {
                            modelo = _mapper.Map<VMVenta>(venta_creada);
                            gResponse.Estado = true;
                            gResponse.Objeto = modelo;
                            gResponse.Mensaje = "Venta registrada. NOTA: Esta factura ya había sido timbrada anteriormente.";
                            return StatusCode(StatusCodes.Status200OK, gResponse);
                        }

                        gResponse.Estado = false;
                        gResponse.Mensaje = $"Venta registrada pero no se pudo timbrar: {resultado.mensaje}";
                        gResponse.Objeto = _mapper.Map<VMVenta>(venta_creada);
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    //  ACTUALIZAR RUTAS EN LA BASE DE DATOS
                    bool actualizacionExitosa = await _ventaServicio.ActualizarRutasFactura(
                        venta_creada.IdVenta,
                        resultado.rutaPDF,
                        resultado.rutaXML,
                        resultado.uuid
                    );

                    if (!actualizacionExitosa)
                    {
                        gResponse.Estado = false;
                        gResponse.Mensaje = "Factura timbrada pero no se pudieron guardar las rutas en la base de datos";
                        return StatusCode(StatusCodes.Status200OK, gResponse);
                    }

                    //  RECARGAR LA VENTA ACTUALIZADA DESDE LA BASE DE DATOS
                    var ventaActualizada = await _ventaServicio.Detalle(venta_creada.NumeroVenta);

                    modelo = _mapper.Map<VMVenta>(ventaActualizada);
                    gResponse.Estado = true;
                    gResponse.Objeto = modelo;
                    gResponse.Mensaje = "Factura timbrada exitosamente";

                }
                else // Ticket
                {
                    Venta venta_creada = await _ventaServicio.Registrar(_mapper.Map<Venta>(modelo));
                    modelo = _mapper.Map<VMVenta>(venta_creada);

                    gResponse.Estado = true;
                    gResponse.Objeto = modelo;
                }
            }
            catch (Exception ex)
            {
                gResponse.Estado = false;
                gResponse.Mensaje = ex.Message;
            }

            return StatusCode(StatusCodes.Status200OK, gResponse);
        }


        [HttpGet]
        public async Task<IActionResult> Historial(string numeroVenta, string fechaInicio, string fechaFin)
        {
            List<VMVenta> vmHistorialVenta = _mapper.Map<List<VMVenta>>(
                await _ventaServicio.Historial(numeroVenta, fechaInicio, fechaFin)
            );

            return StatusCode(StatusCodes.Status200OK, vmHistorialVenta);
        }

        public IActionResult MostrarPDFVenta(string numeroVenta)
        {
            string urlPlantillaVista = $"{this.Request.Scheme}://{this.Request.Host}/Plantilla/PDFVenta?numeroVenta={numeroVenta}";

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = new GlobalSettings()
                {
                    PaperSize = PaperKind.A4,
                    Orientation = Orientation.Portrait,
                },
                Objects = {
                    new ObjectSettings() {
                        Page = urlPlantillaVista
                    }
                }
            };

            var archivoPDF = _converter.Convert(pdf);

            return File(archivoPDF, "application/pdf");
        }

        [HttpGet]
        public async Task<IActionResult> DescargarFacturaPDF(string numeroVenta)
        {
            try
            {
                var venta = await _ventaServicio.Detalle(numeroVenta);

                if (venta == null || string.IsNullOrEmpty(venta.RutaPDF))
                {
                    return NotFound("Factura no encontrada");
                }

                if (!System.IO.File.Exists(venta.RutaPDF))
                {
                    return NotFound("El archivo PDF no existe");
                }

                byte[] pdfBytes = await System.IO.File.ReadAllBytesAsync(venta.RutaPDF);

                return File(pdfBytes, "application/pdf", $"Factura_{numeroVenta}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DescargarFacturaXML(string numeroVenta)
        {
            try
            {
                var venta = await _ventaServicio.Detalle(numeroVenta);

                if (venta == null || string.IsNullOrEmpty(venta.RutaXML))
                {
                    return NotFound("Factura no encontrada");
                }

                if (!System.IO.File.Exists(venta.RutaXML))
                {
                    return NotFound("El archivo XML no existe");
                }

                byte[] xmlBytes = await System.IO.File.ReadAllBytesAsync(venta.RutaXML);

                return File(xmlBytes, "application/xml", $"Factura_{numeroVenta}.xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
        [HttpPost]
        public async Task<IActionResult> SolicitarFactura([FromBody] SolicitudFactura solicitud)
        {
            GenericResponse<object> gResponse = new GenericResponse<object>();

            try
            {
                // 1. Obtener la venta
                var venta = await _ventaServicio.Detalle(solicitud.NumeroVenta);

                if (venta == null)
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = "Venta no encontrada";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 2. Verificar que sea Ticket
                if (venta.IdTipoDocumentoVenta != 1)
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = "Esta venta ya es una factura";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 3. Verificar que tenga cliente
                if (!venta.IdCliente.HasValue)
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = "El ticket no tiene un cliente asignado";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 4. Obtener cliente
                Cliente cliente = await _clienteServicio.Obtener(c => c.IdCliente == venta.IdCliente.Value);

                if (cliente == null)
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = "Cliente no encontrado";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 5. Validar datos fiscales
                if (!_facturacionServicio.ValidarDatosFiscalesCliente(cliente))
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = "El cliente no tiene datos fiscales completos";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 6. Timbrar
                string rutaFacturas = Path.Combine(_env.WebRootPath, "Facturas");
                var resultado = await _facturacionServicio.TimbrarVenta(venta, cliente, rutaFacturas);

                if (!resultado.exito)
                {
                    gResponse.Estado = false;
                    gResponse.Mensaje = $"Error al timbrar: {resultado.mensaje}";
                    return StatusCode(StatusCodes.Status200OK, gResponse);
                }

                // 7. Actualizar rutas
                await _ventaServicio.ActualizarRutasFactura(
                    venta.IdVenta,
                    resultado.rutaPDF,
                    resultado.rutaXML,
                    resultado.uuid
                );

                // 8. Convertir a Factura
                Console.WriteLine($"[SOLICITAR FACTURA] Convirtiendo a factura...");
                bool conversionExitosa = await _ventaServicio.ConvertirTicketAFactura(venta.IdVenta);

                Console.WriteLine($"[SOLICITAR FACTURA] Resultado conversión: {conversionExitosa}");

                if (!conversionExitosa)
                {
                    Console.WriteLine($"[SOLICITAR FACTURA]  Advertencia: No se convirtió a factura");
                    // NO retornar error, continuar de todas formas
                }
                else
                {
                    Console.WriteLine($"[SOLICITAR FACTURA]  Conversión exitosa");
                }


                // 9. Retornar datos
                gResponse.Estado = true;
                gResponse.Mensaje = "Factura generada exitosamente";
                gResponse.Objeto = new
                {
                    numeroVenta = venta.NumeroVenta,
                    uuid = resultado.uuid,
                    rutaPDF = resultado.rutaPDF,
                    rutaXML = resultado.rutaXML
                };

                return StatusCode(StatusCodes.Status200OK, gResponse);
            }
            catch (Exception ex)
            {
                gResponse.Estado = false;
                gResponse.Mensaje = ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, gResponse);
            }
        }

        public class SolicitudFactura
        {
            public string NumeroVenta { get; set; }
            public int IdVenta { get; set; }
        }

    }
}
