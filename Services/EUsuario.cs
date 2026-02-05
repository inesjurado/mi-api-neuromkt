using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;


namespace NeuromktApi.Services
{
    public interface IEUsuario
    {
        Task CrearUsuarioAsync(UsuarioModel usuario);
        Task<List<UsuarioModel>> ListarUsuariosAsync();
        Task ActualizarUsuarioAsync(UsuarioModel usuario, bool? activo = null);
        Task EliminarUsuarioAsync(string email);
        Task<(bool Ok, string? Rol)> LoginAsync(string email, string password);
        Task<UsuarioModel> ObtenerUsuarioAsync(string email);
    }

    public class EUsuario : IEUsuario
    {
        private readonly AppDbContext _db;

        public EUsuario(AppDbContext db)
        {
            _db = db;
        }

        public async Task CrearUsuarioAsync(UsuarioModel u)
        {
            var sql = "SELECT neuromkt.i_usuario(@p_email,@p_nombre,@p_rol,@p_password);";

            var parameters = new[]
            {
                new NpgsqlParameter("@p_email",    u.Email),
                new NpgsqlParameter("@p_nombre",   (object?)u.Nombre ?? DBNull.Value), 
                new NpgsqlParameter("@p_rol",      u.Rol),
                new NpgsqlParameter("@p_password", u.Password)
            };

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, parameters);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        public async Task<List<UsuarioModel>> ListarUsuariosAsync()
        {
            var lista = new List<UsuarioModel>();
            var conn = _db.Database.GetDbConnection();

            try
            {
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM neuromkt.l_usuarios();";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var usuario = new UsuarioModel
                    {
                        Email  = (string)reader["email"],
                        Nombre = (string)reader["nombre"],
                        Rol    = (string)reader["rol"]
                    };

                    lista.Add(usuario);
                }
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }

            return lista;
        }

        public async Task ActualizarUsuarioAsync(UsuarioModel u, bool? activo = null)
        {
            var sql = @"
                SELECT neuromkt.u_usuario(
                    CAST(@p_email    AS varchar),
                    CAST(@p_nombre   AS varchar),
                    CAST(@p_rol      AS varchar),
                    CAST(@p_activo   AS boolean),
                    CAST(@p_password AS varchar)
                );";

            var pEmail = new NpgsqlParameter("@p_email", (object)u.Email);

            var pNombre = new NpgsqlParameter("@p_nombre",
                string.IsNullOrWhiteSpace(u.Nombre)
                    ? (object)DBNull.Value   
                    : u.Nombre);

            var pRol = new NpgsqlParameter("@p_rol",
                string.IsNullOrWhiteSpace(u.Rol)
                    ? (object)DBNull.Value   
                    : u.Rol);

            var pActivo = new NpgsqlParameter("@p_activo",
                (object?)activo ?? DBNull.Value);    

            var pPassword = new NpgsqlParameter("@p_password",
                string.IsNullOrWhiteSpace(u.Password)
                    ? (object)DBNull.Value   
                    : u.Password);

            var parametros = new[] { pEmail, pNombre, pRol, pActivo, pPassword };

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, parametros);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }


        public async Task EliminarUsuarioAsync(string email)
        {
            var sql = "SELECT neuromkt.d_usuario(@p_email);";

            var param = new NpgsqlParameter("@p_email", email);

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, param);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        public async Task<(bool Ok, string? Rol)> LoginAsync(string email, string password)
        {
            const string sql = @"
                SELECT ok, rol
                FROM neuromkt.f_login_usuario(@p_email, @p_password);
            ";

            // Usamos la conexión de EF Core
            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;

            try
            {
                if (!wasOpen)
                    await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_email", email);
                cmd.Parameters.AddWithValue("@p_password", password);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    // La función siempre debería devolver 1 fila,
                    // pero por si acaso devolvemos login fallido.
                    return (false, null);
                }

                var ok = reader.GetBoolean(reader.GetOrdinal("ok"));
                string? rol = reader.IsDBNull(reader.GetOrdinal("rol"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("rol"));

                return (ok, rol);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        public async Task<UsuarioModel> ObtenerUsuarioAsync(string email)
        {
            // Usamos la misma conexión que en ListarUsuariosAsync
            var conn = _db.Database.GetDbConnection();

            try
            {
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT email, nombre, rol
                    FROM neuromkt.usuarios
                    WHERE email = @p_email;
                ";

                var pEmail = cmd.CreateParameter();
                pEmail.ParameterName = "@p_email";
                pEmail.Value = email.Trim().ToLower();
                cmd.Parameters.Add(pEmail);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new Exception($"Usuario {email} no encontrado.");
                }

                return new UsuarioModel
                {
                    Email    = (string)reader["email"],
                    Nombre   = reader["nombre"] as string ?? string.Empty,
                    Rol      = reader["rol"] as string ?? string.Empty,
                    // nunca devolvemos la password real
                    Password = string.Empty
                };
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }





    }
}
