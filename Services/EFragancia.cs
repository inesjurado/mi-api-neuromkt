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
        Task<List<FraganciaModel>> ListarFraganciasAsync();
        Task<string> CrearFraganciaAsync(FraganciaModel fragancia);
        Task ActualizarFraganciaAsync(string codigo, FraganciaModel fragancia);
        Task EliminarFraganciaAsync(string codigo);
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
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }


        // ======================
        // LISTAR FRAGANCIAS
        // ======================
        public async Task<List<FraganciaModel>> ListarFraganciasAsync()
        {
            var lista = new List<FraganciaModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT codigo, nombre, proveedor, descripcion
                    FROM neuromkt.fragancias
                    ORDER BY codigo;";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var f = new FraganciaModel
                    {
                        Codigo      = reader["codigo"]      as string ?? string.Empty,
                        Nombre      = reader["nombre"]      as string ?? string.Empty,
                        Proveedor   = reader["proveedor"]   as string,
                        Descripcion = reader["descripcion"] as string
                    };

                    lista.Add(f);
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

            // Siguiendo tu función PL/pgSQL:
            // - si un parámetro es NULL -> NO se toca ese campo
            // - si es '' en proveedor/descripcion -> se convertirá en NULL
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
    }
}
