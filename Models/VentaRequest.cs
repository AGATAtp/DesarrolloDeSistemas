namespace PrototipoCRUD.Models
{
    // Lo que envía la pantalla al confirmar: cliente + items (el precio y el total los calcula el servidor)
    public class VentaRequest
    {
        public int IdCliente { get; set; }               // 0 = Consumidor Final
        public decimal PorcentajeDescuento { get; set; }
        public List<ItemRequest>? Detalles { get; set; }
    }

    public class ItemRequest
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
    }
}
