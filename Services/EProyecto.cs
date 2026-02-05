using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;

namespace NeuromktApi.Services
{
    public interface IEProyecto
    {
        Task<List<ProyectoModel>> ListarProyectosAsync();
        Task EliminarProyectoAsync(string codigo);
        Task<string> CrearProyectoAsync(ProyectoModel proyecto);
        Task<List<ProyectoModel>> ListarProyectosPorCreadorAsync(string creadoPor);
        Task<ProyectoModel> ObtenerProyectoAsync(string codigo);
        Task ActualizarProyectoAsync(ProyectoModel proyecto);
    }

    public class EProyecto : IEProyecto
    {
        private readonly AppDbContext _db;

        public EProyecto(AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> CrearProyectoAsync(ProyectoModel proyecto)
        {
            const string sql = @"
                SELECT neuromkt.i_proyecto(
                    @p_codigo,
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion,
                    @p_creado_por
                );
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@p_codigo",
                    string.IsNullOrWhiteSpace(proyecto.Codigo)
                        ? (object)DBNull.Value
                        : proyecto.Codigo.Trim());

                cmd.Parameters.AddWithValue("@p_nombre", proyecto.Nombre);
                cmd.Parameters.AddWithValue("@p_proveedor", proyecto.Proveedor);

                cmd.Parameters.AddWithValue("@p_descripcion",
                    string.IsNullOrWhiteSpace(proyecto.Descripcion)
                        ? (object)DBNull.Value
                        : proyecto.Descripcion!.Trim());

                cmd.Parameters.AddWithValue("@p_creado_por", proyecto.CreadoPor);

                var result = await cmd.ExecuteScalarAsync();
                var codigo = Convert.ToString(result);

                return codigo ?? string.Empty;
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


        public async Task<List<ProyectoModel>> ListarProyectosAsync()
        {
            var lista = new List<ProyectoModel>();

            const string sql = @"SELECT * FROM neuromkt.l_proyectos_admin();";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                var ordCodigo = reader.GetOrdinal("codigo");
                var ordNombre = reader.GetOrdinal("nombre");
                var ordProveedor = reader.GetOrdinal("proveedor");
                var ordDescripcion = reader.GetOrdinal("descripcion");
                var ordCreadoPor = reader.GetOrdinal("creado_por");
                var ordFechaCreacion = reader.GetOrdinal("fecha_creacion");
                var ordNumParticipantes = reader.GetOrdinal("num_participantes");

                while (await reader.ReadAsync())
                {
                    lista.Add(new ProyectoModel
                    {
                        Codigo = reader.IsDBNull(ordCodigo) ? "" : reader.GetString(ordCodigo),
                        Nombre = reader.IsDBNull(ordNombre) ? "" : reader.GetString(ordNombre),
                        Proveedor = reader.IsDBNull(ordProveedor) ? "" : reader.GetString(ordProveedor),
                        Descripcion = reader.IsDBNull(ordDescripcion) ? "" : reader.GetString(ordDescripcion),
                        CreadoPor = reader.IsDBNull(ordCreadoPor) ? "" : reader.GetString(ordCreadoPor),
                        FechaCreacion = reader.IsDBNull(ordFechaCreacion) ? DateTime.MinValue : reader.GetDateTime(ordFechaCreacion),
                        NumParticipantes = reader.IsDBNull(ordNumParticipantes) ? 0 : reader.GetInt32(ordNumParticipantes)
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


        public async Task<List<ProyectoModel>> ListarProyectosPorCreadorAsync(string creadoPor)
        {
            var lista = new List<ProyectoModel>();

            const string sql = "SELECT * FROM neuromkt.l_proyectos_por_creador(@p_creado_por);";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_creado_por", creadoPor.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ProyectoModel
                    {
                        Codigo = reader["codigo"] as string ?? string.Empty,
                        Nombre = reader["nombre"] as string ?? string.Empty,
                        Proveedor = reader["proveedor"] as string ?? string.Empty,
                        Descripcion = reader["descripcion"] as string ?? string.Empty,
                        CreadoPor = reader["creado_por"] as string ?? string.Empty,
                        FechaCreacion = reader["fecha_creacion"] is DBNull ? default : (DateTime)reader["fecha_creacion"],
                        NumParticipantes = reader["num_participantes"] is DBNull ? 0 : Convert.ToInt32(reader["num_participantes"])
                    });
                }
            }
            finally
            {
                if (!wasOpen) await conn.CloseAsync();
            }

            return lista;
        }

        public async Task EliminarProyectoAsync(string codigo)
        {
            var sql = "DELETE FROM neuromkt.proyectos WHERE codigo = @p_codigo;";
            var param = new NpgsqlParameter("@p_codigo", codigo);

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


        public async Task<ProyectoModel> ObtenerProyectoAsync(string codigo)
        {
            const string sql = @"
                SELECT codigo, nombre, proveedor, descripcion, creado_por
                FROM neuromkt.proyectos
                WHERE codigo = @p_codigo;
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_codigo", codigo);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new Exception($"Proyecto '{codigo}' no encontrado.");

                return new ProyectoModel
                {
                    Codigo     = reader.GetString(0),
                    Nombre     = reader.GetString(1),
                    Proveedor  = reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreadoPor  = reader.GetString(4)
                };
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        public async Task ActualizarProyectoAsync(ProyectoModel proyecto)
        {
            const string sql = @"
                UPDATE neuromkt.proyectos
                SET nombre     = @p_nombre,
                    proveedor  = @p_proveedor,
                    descripcion = @p_descripcion
                WHERE codigo   = @p_codigo;
            ";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@p_codigo", proyecto.Codigo);
                cmd.Parameters.AddWithValue("@p_nombre", proyecto.Nombre);
                cmd.Parameters.AddWithValue("@p_proveedor", proyecto.Proveedor);
                cmd.Parameters.AddWithValue("@p_descripcion",
                    string.IsNullOrWhiteSpace(proyecto.Descripcion)
                        ? (object)DBNull.Value
                        : proyecto.Descripcion);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

    }
}
