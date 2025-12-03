namespace NeuromktApi.Models
{
    public class ProyectoFraganciaModel
    {
        // PK de la tabla proyectos_fragancias
        public string Codigo { get; set; } = string.Empty;

        // FK al proyecto
        public string ProyectoCodigo { get; set; } = string.Empty;

        // Código de la fragancia (FK a fragancias.codigo)
        public string FraganciaCodigo { get; set; } = string.Empty;
    }
}
