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
        Task CrearProyectoAsync(ProyectoModel proyecto);
        Task<List<ProyectoResumenModel>> ListarProyectosResumenAsync();
    }

    public class EProyecto : IEProyecto
    {
        private readonly AppDbContext _db;

        public EProyecto(AppDbContext db)
        {
            _db = db;
        }

        public async Task CrearProyectoAsync(ProyectoModel proyecto)
        {
            var sql = @"
                SELECT neuromkt.i_proyecto(
                    @p_codigo,
                    @p_nombre,
                    @p_proveedor,
                    @p_descripcion,
                    @p_creado_por
                );";

            var pCodigo = new NpgsqlParameter("@p_codigo",
                string.IsNullOrWhiteSpace(proyecto.Codigo)
                    ? (object)DBNull.Value
                    : proyecto.Codigo);

            var pNombre     = new NpgsqlParameter("@p_nombre",     proyecto.Nombre);
            var pProveedor  = new NpgsqlParameter("@p_proveedor",  proyecto.Proveedor);
            var pDescripcion = new NpgsqlParameter("@p_descripcion",
                string.IsNullOrWhiteSpace(proyecto.Descripcion)
                    ? (object)DBNull.Value
                    : proyecto.Descripcion);

            var pCreadoPor  = new NpgsqlParameter("@p_creado_por", proyecto.CreadoPor);

            var parametros = new[] { pCodigo, pNombre, pProveedor, pDescripcion, pCreadoPor };

            Console.WriteLine($"[DEBUG] CrearProyecto: Nombre={proyecto.Nombre}, Proveedor={proyecto.Proveedor}, CreadoPor={proyecto.CreadoPor}");

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

        // =======================
        // LISTAR PROYECTOS (ADMIN)
        // =======================
        public async Task<List<ProyectoModel>> ListarProyectosAsync()
        {
            var lista = new List<ProyectoModel>();

            // NO usar using aquí
            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM neuromkt.l_proyectos();";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var p = new ProyectoModel
                    {
                        Codigo        = reader["codigo"]        as string ?? string.Empty,
                        Nombre        = reader["nombre"]        as string ?? string.Empty,
                        Proveedor     = reader["proveedor"]     as string ?? string.Empty,
                        Descripcion   = reader["descripcion"]   as string ?? string.Empty,
                        CreadoPor     = reader["creado_por"]    as string ?? string.Empty,
                        FechaCreacion = (DateTime)reader["fecha_creacion"]
                    };

                    lista.Add(p);
                }
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
            finally
            {
                // Solo cerramos si la abrimos nosotros
                if (!wasOpen)
                    await conn.CloseAsync();
            }

            return lista;
        }

        public async Task EliminarProyectoAsync(string codigo)
        {
            // Más adelante puedes cambiarlo a d_proyecto(codigo)
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

        // =========================================
        // LISTAR PROYECTOS RESUMEN (INICIO ADMIN)
        // =========================================
        public async Task<List<ProyectoResumenModel>> ListarProyectosResumenAsync()
        {
            var lista = new List<ProyectoResumenModel>();

            const string sql = "SELECT * FROM neuromkt.l_proyectos_resumen();";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var item = new ProyectoResumenModel
                    {
                        Codigo = reader["codigo"] as string ?? string.Empty,
                        Nombre = reader["nombre"] as string ?? string.Empty,
                        NumParticipantes = reader["num_participantes"] is DBNull
                            ? 0
                            : Convert.ToInt32(reader["num_participantes"])
                    };

                    lista.Add(item);
                }
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }

            return lista;
        }
    }
}
