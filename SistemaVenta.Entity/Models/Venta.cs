using System;
using System.Collections.Generic;

namespace SistemaVenta.Entity.Models;

public partial class Venta
{
    public int IdVenta { get; set; }

    public string? NumeroVenta { get; set; }

    public int? IdTipoDocumentoVenta { get; set; }

    public int? IdUsuario { get; set; }

    public string? DocumentoCliente { get; set; }

    public string? NombreCliente { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? ImpuestoTotal { get; set; }

    public decimal? Total { get; set; }

    public DateTime? FechaRegistro { get; set; }

    //  CAMPOS NUEVOS PARA FACTURACIÓN
    public int? IdCliente { get; set; }

    public string? RutaXML { get; set; }

    public string? RutaPDF { get; set; }

    public string? UUID { get; set; }

    public DateTime? FechaTimbrado { get; set; }

    // Navegación
    public virtual ICollection<DetalleVenta> DetalleVenta { get; set; } = new List<DetalleVenta>();

    public virtual TipoDocumentoVenta? IdTipoDocumentoVentaNavigation { get; set; }

    public virtual Usuario? IdUsuarioNavigation { get; set; }

    public virtual Cliente? IdClienteNavigation { get; set; }
}
