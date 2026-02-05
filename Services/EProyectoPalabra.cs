using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeuromktApi.Models;
using Npgsql;
using System.Data;


namespace NeuromktApi.Services
{
    public interface IEProyectoPalabra
    {
        Task CrearProyectoPalabraAsync(string proyectoCodigo, string palabra);
        Task<List<ProyectoPalabraModel>> ListarPalabrasPorProyectoAsync(string proyectoCodigo);
        Task EliminarPorProyectoAsync(string proyectoCodigo);
    }

    public class EProyectoPalabra : IEProyectoPalabra
    {
        private readonly AppDbContext _db;

        public EProyectoPalabra(AppDbContext db)
        {
            _db = db;
        }

        public async Task CrearProyectoPalabraAsync(string proyectoCodigo, string palabra)
        {
            const string sql = @"
                SELECT neuromkt.i_proyecto_palabra(
                    @p_proyecto_codigo,
                    @p_palabra,
                    @p_codigo
                );";

            var pProyecto = new NpgsqlParameter("@p_proyecto_codigo", proyectoCodigo.Trim());
            var pPalabra  = new NpgsqlParameter("@p_palabra", palabra.Trim());
            var pCodigo   = new NpgsqlParameter("@p_codigo", DBNull.Value); 

            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, pProyecto, pPalabra, pCodigo);
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"[Postgres] {ex.MessageText}");
                throw;
            }
        }

        public async Task<List<ProyectoPalabraModel>> ListarPalabrasPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"
                SELECT codigo, palabra
                FROM neuromkt.f_proyecto_palabras(@p_proyecto_codigo);
            ";

            var lista = new List<ProyectoPalabraModel>();

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new ProyectoPalabraModel
                    {
                        Codigo  = reader.GetString(0),
                        Palabra = reader.GetString(1),
                        ProyectoCodigo = proyectoCodigo  
                    });
                }

                return lista;
            }
            finally
            {
                if (!wasOpen)
                    await conn.CloseAsync();
            }
        }

        public async Task EliminarPorProyectoAsync(string proyectoCodigo)
        {
            const string sql = @"SELECT neuromkt.d_proyecto_palabras(@p_proyecto_codigo);";

            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
                await conn.OpenAsync();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@p_proyecto_codigo", proyectoCodigo);
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