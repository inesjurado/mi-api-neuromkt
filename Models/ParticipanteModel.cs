namespace NeuromktApi.Models
{
    public class ParticipanteModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime? FechaNacimiento { get; set; }
        public string? Genero { get; set; }
        public string? Notas { get; set; }
        public string NombreCompleto => Email;
        public string CreadoPor { get; set; } = string.Empty;
    }
}
