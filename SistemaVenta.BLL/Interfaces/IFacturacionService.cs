using SistemaVenta.Entity.Models;
using System.Threading.Tasks;

namespace SistemaVenta.BLL.Interfaces
{
    public interface IFacturacionService
    {
        /// <summary>
        /// Timbra una venta ante el SAT y extrae el PDF
        /// </summary>
        Task<(bool exito, string rutaPDF, string rutaXML, string uuid, string mensaje)> TimbrarVenta(
            Venta venta,
            Cliente cliente,
            string rutaGuardado
        );

        /// <summary>
        /// Valida que el cliente tenga todos los datos fiscales requeridos
        /// </summary>
        bool ValidarDatosFiscalesCliente(Cliente cliente);

        /// <summary>
        /// Valida que el negocio tenga configuración completa para timbrar
        /// </summary>
        Task<bool> ValidarConfiguracionNegocio();
    }
}
