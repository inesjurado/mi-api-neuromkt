// Models/UsuarioModel.cs
namespace NeuromktApi.Models
{
    public class UsuarioModel
    {
        public string Email { get; set; } = string.Empty;
        public string Nombre { get; set; }= string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
