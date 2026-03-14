using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SistemaVenta.AplicacionWeb.Models.ViewModels;
using SistemaVenta.AplicacionWeb.Utilidades.Response;
using SistemaVenta.BLL.Interfaces;
using SistemaVenta.Entity.Models;

namespace SistemaVenta.AplicacionWeb.Controllers
{
    [Authorize]
    public class ClienteController : Controller
    {
        private readonly IClienteService _clienteServicio;
        private readonly IMapper _mapper;

        public ClienteController(
            IClienteService clienteServicio,
            IMapper mapper
            )
        {
            _clienteServicio = clienteServicio;
            _mapper = mapper;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Lista()
        {
            List<VMCliente> vmClienteLista = _mapper.Map<List<VMCliente>>(await _clienteServicio.Lista());
            return StatusCode(StatusCodes.Status200OK, new { data = vmClienteLista });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPorId(int clienteId)
        {
            VMCliente vmClienteLista = _mapper.Map<VMCliente>(await _clienteServicio.ObtenerPorId(clienteId));
            return StatusCode(StatusCodes.Status200OK, new { data = vmClienteLista });
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] string modelo)
        {
            GenericResponse<VMCliente> gResponse = new GenericResponse<VMCliente>();

            try
            {
                VMCliente vmCliente = JsonConvert.DeserializeObject<VMCliente>(modelo);

                Cliente cliente_creado = await _clienteServicio.Crear(_mapper.Map<Cliente>(vmCliente));

                vmCliente = _mapper.Map<VMCliente>(cliente_creado);

                gResponse.Estado = true;
                gResponse.Objeto = vmCliente;
            }
            catch (Exception ex)
            {
                gResponse.Estado = false;
                gResponse.Mensaje = ex.Message;
            }

            return StatusCode(StatusCodes.Status200OK, gResponse);
        }

        [HttpPut]
        public async Task<IActionResult> Editar([FromForm] string modelo)
        {
            GenericResponse<VMCliente> gResponse = new GenericResponse<VMCliente>();

            try
            {
                VMCliente vmCliente = JsonConvert.DeserializeObject<VMCliente>(modelo);

                Cliente cliente_editado = await _clienteServicio.Editar(_mapper.Map<Cliente>(vmCliente));

                vmCliente = _mapper.Map<VMCliente>(cliente_editado);

                gResponse.Estado = true;
                gResponse.Objeto = vmCliente;

            }
            catch (Exception ex)
            {
                gResponse.Estado = false;
                gResponse.Mensaje = ex.Message;
            }

            return StatusCode(StatusCodes.Status200OK, gResponse);
        }

        [HttpDelete]
        public async Task<IActionResult> Eliminar(int idCLiente)
        {
            GenericResponse<string> gResponse = new GenericResponse<string>();

            try
            {
                gResponse.Estado = await _clienteServicio.Eliminar(idCLiente);
            }
            catch (Exception ex)
            {
                gResponse.Estado = false;
                gResponse.Mensaje = ex.Message;
            }

            return StatusCode(StatusCodes.Status200OK, gResponse);
        }

        // ================================================
        // MÉTODO CORREGIDO
        // ================================================
        [HttpGet]
        public async Task<IActionResult> BuscarClientePorNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return Json(null);

            nombre = nombre.Trim().ToLower();

            // Buscar coincidencia parcial ignorando mayúsculas
            var cliente = await _clienteServicio.Obtener(
                c => c.EsActivo == true &&
                     c.Nombre.ToLower().Contains(nombre)
            );

            if (cliente == null)
                return Json(null);

            var dto = _mapper.Map<ClienteDTO>(cliente);

            return Json(dto);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string busqueda)
        {
            if (string.IsNullOrWhiteSpace(busqueda))
                return Json(new List<object>());

            busqueda = busqueda.Trim().ToLower();

            // Obtener todos los clientes (o filtrar desde el servicio si ya tienes método)
            var clientes = await _clienteServicio.Lista();

            var lista = clientes
                .Where(c => c.Nombre.ToLower().Contains(busqueda))
                .Select(c => new {
                    idCliente = c.IdCliente,
                    nombreCompleto = c.Nombre,
                    correo = c.Correo,
                    rfc = c.Rfc,
                    domicilioFiscalReceptor = c.DomicilioFiscalReceptor,
                    regimenFiscalReceptor = c.RegimenFiscalReceptor,
                    fechaRegistro = c.FechaRegistro.ToString("yyyy-MM-dd")
                })
                .ToList();

            return Json(lista);
        }





    }
}
