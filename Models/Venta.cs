namespace PrototipoCRUD.Models
{
    public class Venta
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public int IdCliente { get; set; }
        public int? IdVendedor { get; set; }
        public string Cliente { get; set; } = "Consumidor Final";
        public string Vendedor { get; set; } = "Sin registrar";
        public decimal PorcentajeDescuento { get; private set; }
        public decimal SubtotalBruto => Detalles.Sum(d => d.Subtotal);
        public decimal MontoDescuento { get; private set; }
        public decimal TotalFinal => SubtotalBruto - MontoDescuento;
        public decimal Total => TotalFinal;
        public List<DetalleVenta> Detalles { get; set; } = new();

        public void AplicarDescuento(decimal porcentaje)
        {
            if (porcentaje < 0 || porcentaje > 20)
                throw new ArgumentOutOfRangeException(nameof(porcentaje), "El descuento debe estar entre 0% y 20%.");

            PorcentajeDescuento = porcentaje;
            MontoDescuento = Math.Round(SubtotalBruto * (porcentaje / 100m), 2, MidpointRounding.AwayFromZero);
        }
    }
}
