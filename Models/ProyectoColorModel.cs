namespace NeuromktApi.Models
{
    public class ProyectoColorModel
    {
        // PK de la tabla proyectos_colores
        public string Codigo { get; set; } = string.Empty;

        // FK al proyecto
        public string ProyectoCodigo { get; set; } = string.Empty;

        // Código HEX del color (#RRGGBB)
        public string ColorHex { get; set; } = string.Empty;
    }
}
