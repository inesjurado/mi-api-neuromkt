using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NeuromktApi.Services
{
    public interface IExportService
    {
        Task DescargarCsvAsync<T>(
            IEnumerable<T> datos,
            string fileName,
            string separador = ";");
    }

    public class ExportService : IExportService
    {
        private readonly IJSRuntime _js;

        public ExportService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task DescargarCsvAsync<T>(
            IEnumerable<T> datos,
            string fileName,
            string separador = ";")
        {
            var lista = datos?.ToList() ?? new List<T>();
            if (!lista.Any())
            {
                return;
            }

            var props = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray();

            var sb = new StringBuilder();

            sb.AppendLine(string.Join(separador, props.Select(p => Escapar(p.Name, separador))));

            foreach (var item in lista)
            {
                var valores = props.Select(p =>
                {
                    var val = p.GetValue(item, null);
                    var txt = val?.ToString() ?? "";
                    return Escapar(txt, separador);
                });

                sb.AppendLine(string.Join(separador, valores));
            }

            var csv = sb.ToString();
            await _js.InvokeVoidAsync("downloadHelper.downloadText", fileName, csv);
        }

        private static string Escapar(string value, string separador)
        {
            var necesitaComillas =
                value.Contains(separador) ||
                value.Contains("\"") ||
                value.Contains("\r") ||
                value.Contains("\n");

            if (!necesitaComillas)
                return value;

            var conDobles = value.Replace("\"", "\"\"");
            return $"\"{conDobles}\"";
        }
    }
}
