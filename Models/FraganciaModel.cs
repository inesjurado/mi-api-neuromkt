namespace NeuromktApi.Models
{
    public class FraganciaModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Proveedor { get; set; }
        public string? Descripcion { get; set; }
        public string CreadoPor { get; set; } = string.Empty;

    }
}

