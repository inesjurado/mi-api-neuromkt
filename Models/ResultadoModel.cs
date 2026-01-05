namespace NeuromktApi.Models
{
    public class ResultadoModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string PruebaCodigo { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
        public string Palabra { get; set; } = string.Empty;
        public string UsuarioEmail { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;   // "color" o "palabra"
        public string Valor { get; set; } = string.Empty;  // "#FF00AA" o "Fresco", etc.
        public int Total { get; set; }
    }
}
