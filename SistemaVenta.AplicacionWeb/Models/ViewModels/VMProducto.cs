using SistemaVenta.Entity;

namespace SistemaVenta.AplicacionWeb.Models.ViewModels
{
    public class VMProducto
    {
        public int IdProducto { get; set; }

        public string? CodigoBarra { get; set; }

        public string? Marca { get; set; }

        public string? Descripcion { get; set; }

        public int? IdCategoria { get; set; }

        public string? NombreCategoria { get; set; }

        public int? Stock { get; set; }

        public string? UrlImagen { get; set; }

        public string? Precio { get; set; }

        public int? EsActivo { get; set; }

        public string? MedidaEmpresa { get; set; }

        public string? MedidaSat { get; set; }

        public string? ClaveProductoSat { get; set; }

        public string? ObjetoImpuesto { get; set; }

        public string? FactorImpuesto { get; set; }

        public string? Impuesto { get; set; }

        public decimal? ValorImpuesto { get; set; }

        public string? TipoImpuesto { get; set; }

        public decimal? Descuento { get; set; }
    }
}