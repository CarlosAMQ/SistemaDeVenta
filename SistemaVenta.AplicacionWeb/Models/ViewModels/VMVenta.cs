using System;
using System.Collections.Generic;

namespace SistemaVenta.AplicacionWeb.Models.ViewModels
{
    public class VMVenta
    {
        public int IdVenta { get; set; }

        public string? NumeroVenta { get; set; }

        public int? IdTipoDocumentoVenta { get; set; }

        public string? TipoDocumentoVenta { get; set; }

        public int? IdUsuario { get; set; }

        public string? Usuario { get; set; }

        public string? DocumentoCliente { get; set; }

        public string? NombreCliente { get; set; }

        public string? SubTotal { get; set; }

        public string? ImpuestoTotal { get; set; }

        public string? Total { get; set; }

        public string? FechaRegistro { get; set; }

        public int? IdCliente { get; set; }

        public string? RutaPDF { get; set; }

        public string? RutaXML { get; set; }

        public string? UUID { get; set; }

        public List<VMDetalleVenta>? DetalleVenta { get; set; }
    }
}
