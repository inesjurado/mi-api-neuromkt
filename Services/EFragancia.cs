using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEFragancia
    {
        Task<List<FraganciaModel>> ListarFraganciasPorCreadorAsync(string creadoPor);
        Task<string> CrearFraganciaAsync(FraganciaModel fragancia);
        Task ActualizarFraganciaAsync(string codigo, FraganciaModel fragancia);
        Task EliminarFraganciaAsync(string codigo);
        Task<FraganciaModel?> ObtenerFraganciaAsync(string codigo);
    }

    public class EFragancia : IEFragancia
    {
        private readonly AppDbContext _db;

        public EFragancia(AppDbContext db)
        {
            _db = db;
        }

        // ======================
        // INSERTAR FRAGANCIA
        // ======================
        public async Task<string> CrearFraganciaAsync(FraganciaModel f)
        {
            const string sql = @"
                SELECT neuromkt.i_fragancia(
                    @p_codigo,
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion,
                    @p_creado_por
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@p_codigo",
                    string.IsNullOrWhiteSpace(f.Codigo) ? (object)DBNull.Value : f.Codigo.Trim());

                cmd.Parameters.AddWithValue("@p_nombre", f.Nombre);
                cmd.Parameters.AddWithValue("@p_proveedor", f.Proveedor ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@p_descripcion",
                    string.IsNullOrWhiteSpace(f.Descripcion) ? (object)DBNull.Value : f.Descripcion.Trim());

                cmd.Parameters.AddWithValue("@p_creado_por", f.CreadoPor); // 👈 nuevo campo

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToString(result) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR ListarFraganciasAsync: " + ex.Message);
                Console.WriteLine("INNER: " + ex.InnerException?.Message);
                Console.WriteLine(ex.ToString());
                throw;
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }


        // ======================
        // LISTAR FRAGANCIAS
        // ======================
        public async Task<List<FraganciaModel>> ListarFraganciasPorCreadorAsync(string creadoPor)
        {
            if (string.IsNullOrWhiteSpace(creadoPor))
                throw new Exception("creadoPor es obligatorio para listar fragancias.");

            var lista = new List<FraganciaModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                const string sql = "SELECT codigo, nombre, proveedor, descripcion, creado_por FROM neuromkt.f_fragancias_por_creador(:p_creado_por);";

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("p_creado_por", creadoPor.Trim().ToLower());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var codigo = reader.GetString(0);
                    var nombre = reader.GetString(1);

                    var proveedor = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var descripcion = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    var creado = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

                    lista.Add(new FraganciaModel
                    {
                        Codigo = codigo,
                        Nombre = nombre,
                        Proveedor = proveedor,
                        Descripcion = descripcion,
                        CreadoPor = creado
                    });
                }
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

            return lista;
        }

        // ======================
        // ACTUALIZAR FRAGANCIA
        // ======================
        public async Task ActualizarFraganciaAsync(string codigo, FraganciaModel fragancia)
        {
            const string sql = @"
                SELECT neuromkt.u_fragancia(
                    @p_codigo,
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion
                );";

            var pCodigo = new NpgsqlParameter("@p_codigo", codigo?.Trim() ?? string.Empty);
            var pNombre = new NpgsqlParameter("@p_nombre",
                (object?)fragancia.Nombre ?? DBNull.Value);

            var pProveedor = new NpgsqlParameter("@p_proveedor",
                (object?)fragancia.Proveedor ?? DBNull.Value);

            var pDescripcion = new NpgsqlParameter("@p_descripcion",
                (object?)fragancia.Descripcion ?? DBNull.Value);

            var parametros = new[] { pCodigo, pNombre, pProveedor, pDescripcion };

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

        // ======================
        // ELIMINAR FRAGANCIA
        // ======================
        public async Task EliminarFraganciaAsync(string codigo)
        {
            const string sql = @"SELECT neuromkt.d_fragancia(@p_codigo);";

            var param = new NpgsqlParameter("@p_codigo", codigo?.Trim() ?? string.Empty);

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

        public async Task<FraganciaModel?> ObtenerFraganciaAsync(string codigo)
        {
            const string sql = @"
                SELECT codigo, nombre, proveedor, descripcion, creado_por
                FROM neuromkt.fragancias
                WHERE codigo = @p_codigo
                LIMIT 1;
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_codigo", codigo.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                return new FraganciaModel
                {
                    Codigo = reader["codigo"] as string ?? "",
                    Nombre = reader["nombre"] as string ?? "",
                    Proveedor = reader["proveedor"] as string,
                    Descripcion = reader["descripcion"] as string,
                    CreadoPor = reader["creado_por"] as string
                };
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }
        }

    }
}
