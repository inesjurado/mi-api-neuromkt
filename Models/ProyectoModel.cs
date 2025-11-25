namespace NeuromktApi.Models
{
    public class ProyectoModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Proveedor { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string CreadoPor { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
    }
}
