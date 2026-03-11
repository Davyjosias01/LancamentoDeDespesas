using LancamentoDeDespesas.ObjectRepository;
using System;
using System.Collections.Generic;
using System.Data;
using UiPath.Activities.System.Jobs.Coded;
using UiPath.CodedWorkflows;
using UiPath.Core;
using UiPath.Core.Activities.Storage;
using UiPath.Orchestrator.Client.Models;
using UiPath.UIAutomationNext.API.Contracts;
using UiPath.UIAutomationNext.API.Models;
using UiPath.UIAutomationNext.Enums;
using System.Linq;

namespace ParametrizacaoDeEmpresas;
    public static class CnaeHelper {
        public static List<int> GetDistinctAnnexes(List<string> companyCnaes, DataTable dtCnaes) {
            
            if (dtCnaes == null) throw new ArgumentNullException(nameof(dtCnaes));
            if (companyCnaes == null || companyCnaes.Count == 0) return new List<int>();

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
                ["I"] = 1, ["II"] = 2, ["III"] = 3, ["IV"] = 4, ["V"] = 5
            };

        var cnaeSet = new HashSet<string>(companyCnaes.Where(s => !string.IsNullOrWhiteSpace(s))
                                                      .Select(s => s.Trim()),
                                          StringComparer.Ordinal);

        var result = new HashSet<int>();

        foreach (DataRow row in dtCnaes.Rows) {
            var cod = row["cnae"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(cod) || !cnaeSet.Contains(cod))
                continue;

            var raw = row["anexos"] == DBNull.Value ? string.Empty : row["anexos"]?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            // Divide por vírgula, limpa aspas/espaços, normaliza
            foreach (var piece in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                var cleaned = piece.Replace("\"", "").Trim().ToUpperInvariant();
                if (map.TryGetValue(cleaned, out var annexNumber)) {
                    result.Add(annexNumber);
                }
            }
        }

        return result.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// (Opcional) Mesma lógica, retornando "I", "II", "III", "IV", "V" distintos.
        /// </summary>
        public static List<string> GetDistinctAnnexesRoman(List<string> companyCnaes, DataTable dtCnaes) {
            if (dtCnaes == null) throw new ArgumentNullException(nameof(dtCnaes));
            if (companyCnaes == null || companyCnaes.Count == 0) return new List<string>();

            var valid = new HashSet<string>(new[] { "I", "II", "III", "IV", "V" }, StringComparer.OrdinalIgnoreCase);
            var cnaeSet = new HashSet<string>(companyCnaes.Where(s => !string.IsNullOrWhiteSpace(s))
                                                      .Select(s => s.Trim()),
                                          StringComparer.Ordinal);

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in dtCnaes.Rows) {
                var cod = row["cnae"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(cod) || !cnaeSet.Contains(cod))
                    continue;
                var raw = row["anexos"] == DBNull.Value ? string.Empty : row["anexos"]?.ToString();
                
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var piece in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                    var cleaned = piece.Replace("\"", "").Trim().ToUpperInvariant();
                    if (valid.Contains(cleaned))
                        result.Add(cleaned);
                }
            }

        // Ordena por valor numérico I..V
        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        { ["I"]=1, ["II"]=2, ["III"]=3, ["IV"]=4, ["V"]=5 };

        return result.OrderBy(r => orderMap[r]).ToList();
    }
}