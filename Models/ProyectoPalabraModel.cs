namespace NeuromktApi.Models
{
    public class ProyectoPalabraModel
    {
        // PK de la tabla proyectos_palabras
        public string Codigo { get; set; } = string.Empty;

        // FK al proyecto
        public string ProyectoCodigo { get; set; } = string.Empty;

        // Palabra (FK a Palabras.Palabra)
        public string Palabra { get; set; } = string.Empty;
    }
}
