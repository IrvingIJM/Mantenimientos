using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mantenimientos.Helpers
{
    public class SucursalCandidata
    {
        public string ClvSuc { get; set; } = string.Empty;
        public string NombreBD { get; set; } = string.Empty;
    }

    public class ResultadoCoincidencia
    {
        public SucursalCandidata? Sucursal { get; set; }
        public double Score { get; set; }
        public bool EsAmbiguo { get; set; }
        public bool EsConfiable => Score >= 0.70 && !EsAmbiguo;
    }

    public static class SucursalMatcher
    {
        // 1. CAPA DE ALIAS: Casos del Excel que cambian estructuralmente respecto a la BD o carecen de información vital
        private static readonly Dictionary<string, string> CasosEspecialesExcel = new(StringComparer.OrdinalIgnoreCase)
        {
            // Fusiones y sufijos cambiados
            { "bimbo y barcel cordoba", "cordoba" },
            { "bimbo-barcel culiacán bugambilias", "barcel culiacan" },
            { "ceve juarez auto (fusion ruiseñor)", "juarez auto nuevo canal" },
            { "barcel tijuana terrazas", "barcel tijuana presa" },
            { "barcel tuxtla gutierrez", "barcel tuxtla" },
            { "mexicali plaza (zahuaro)", "mexicali plaza" },
            { "ceve san jose iturbide (int.)", "san jose iturbide intermedio" },
            { "culiacán bachigualato", "culiacan bachigualatos" },
            
            // Faltas de palabras clave o nombres oficiales distintos
            { "bilbao", "san lorenzo bilbao" },
            { "ceve barcel león", "barcel leon san miguel" },
            { "ceve huejutla", "huejutla de reyes" },
            { "hermosillo sur", "bimbo hermosillo sur" },
            { "mazatlán sur", "mazatlan sur marinela" },
            { "ceve fresnillo", "bimbo fresnillo" },
            { "bimbo r. michel", "r. michel" },
            { "ceve cuahutemoc", "cuauhtemoc" },
            
            // Reordenamientos y typos de la base de datos
            { "tlaquepaque cedis", "tlaqueparque cedis" },
            { "ceve marinela reynosa", "reynosa marinela" },
            { "ceve reynosa bimbo y barcel", "reynosa bimbo" },
            { "ceve morelia abastos norte", "morelia norte" },
            { "ceve saltillo", "saltillo bimbo" },
            { "villahermosa ind", "villahermosa industrial" },
            { "barcel puebla 14 sur", "barcel puebla sur" },
            { "11 sur", "puebla 11 sur" },
            { "acayucan cedis", "acayucan" }
        };

        // 2. DICCIONARIO DE TRADUCCIÓN Y RUIDO
        private static readonly Dictionary<string, string> Abreviaturas = new(StringComparer.OrdinalIgnoreCase)
        {
            { "cd.", "ciudad" }, { "cd", "ciudad" },{ "ind", "industrial" }, { "int.", "intermedio" }, { "int", "intermedio" },
            { "intermedio", "intermedio" }, { "auto", "auto" },
            // Palabras de ruido que se descartan
            { "ceve", "" }, { "planta", "" }
        };

        public static ResultadoCoincidencia BuscarMejorCoincidencia(string textoExcel, IEnumerable<SucursalCandidata> sucursalesBD)
        {
            if (string.IsNullOrWhiteSpace(textoExcel) || sucursalesBD == null || !sucursalesBD.Any())
                return new ResultadoCoincidencia { Score = 0 };

            string excelLimpio = textoExcel.Trim();

            // CAPA 1: Búsqueda exacta por Diccionario de Alias (100% de seguridad)
            if (CasosEspecialesExcel.TryGetValue(excelLimpio, out string? nombreBDEsperado))
            {
                var sucursalAlias = sucursalesBD.FirstOrDefault(s => NormalizarTextoBasico(s.NombreBD) == NormalizarTextoBasico(nombreBDEsperado));
                if (sucursalAlias != null)
                {
                    return new ResultadoCoincidencia { Sucursal = sucursalAlias, Score = 1.0, EsAmbiguo = false };
                }
            }

            // CAPA 2: Normalización Avanzada
            string excelNorm = NormalizarTexto(textoExcel);
            var tokensExcel = ObtenerTokens(excelNorm);

            // Coincidencia Exacta post-normalización
            var exactas = sucursalesBD.Where(s => NormalizarTexto(s.NombreBD) == excelNorm).ToList();
            if (exactas.Count == 1) return new ResultadoCoincidencia { Sucursal = exactas[0], Score = 1.0, EsAmbiguo = false };

            // CAPA 3: Motor Difuso Basado en Tokens
            var resultados = new List<(SucursalCandidata Sucursal, double Score)>();

            foreach (var suc in sucursalesBD)
            {
                string bdNorm = NormalizarTexto(suc.NombreBD);
                var tokensBD = ObtenerTokens(bdNorm);

                double jaccard = CalcularSimilitudJaccard(tokensExcel, tokensBD);
                double levenshtein = CalcularSimilitudLevenshtein(excelNorm, bdNorm);

                // 70% peso a coincidencia exacta de palabras, 30% a ortografía
                double scoreFinal = (jaccard * 0.70) + (levenshtein * 0.30);

                if (EsConflictoDeMarcas(excelNorm, bdNorm))
                {
                    scoreFinal -= 0.50;
                }

                if (scoreFinal > 0.40)
                {
                    resultados.Add((suc, scoreFinal));
                }
            }

            if (resultados.Count == 0) return new ResultadoCoincidencia { Score = 0 };

            var ordenados = resultados.OrderByDescending(r => r.Score).ToList();
            var mejor = ordenados[0];

            // CAPA 4: Control de Ambigüedad
            bool esAmbiguo = false;
            if (ordenados.Count > 1)
            {
                var segundo = ordenados[1];
                if (mejor.Score >= 0.60 && (mejor.Score - segundo.Score) < 0.08)
                {
                    esAmbiguo = true;
                    // Desempate si el Excel menciona "CEDIS"
                    if (excelNorm.Contains("cedis"))
                    {
                        var matchCedis = ordenados.FirstOrDefault(x => NormalizarTexto(x.Sucursal.NombreBD).Contains("cedis"));
                        if (matchCedis.Sucursal != null)
                        {
                            mejor = matchCedis;
                            esAmbiguo = false;
                        }
                    }
                }
            }

            return new ResultadoCoincidencia
            {
                Sucursal = mejor.Sucursal,
                Score = mejor.Score,
                EsAmbiguo = esAmbiguo
            };
        }

        private static string NormalizarTextoBasico(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            string limpio = RemoveDiacritics(texto.ToLowerInvariant());
            return Regex.Replace(limpio, @"[^a-z0-9]", "").Trim();
        }

        public static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            // Se quitó la expresión regular que eliminaba por completo el contenido de los paréntesis
            string textoSinTildes = RemoveDiacritics(texto.ToLowerInvariant());

            // Reemplaza paréntesis y cualquier carácter especial por un espacio, salvando las palabras internas
            string limpio = Regex.Replace(textoSinTildes, @"[^a-z0-9\s]", " ");

            var palabras = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => Abreviaturas.TryGetValue(p, out string? exp) ? exp : p)
                                 .Where(p => !string.IsNullOrWhiteSpace(p) &&
                                             p != "y" && p != "de" && p != "del" &&
                                             p != "la" && p != "el" && p != "los" && p != "las");

            return string.Join(" ", palabras);
        }

        private static HashSet<string> ObtenerTokens(string textoNormalizado)
        {
            return new HashSet<string>(textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool EsConflictoDeMarcas(string text1, string text2)
        {
            bool t1Bimbo = text1.Contains("bimbo");
            bool t1Barcel = text1.Contains("barcel");
            bool t2Bimbo = text2.Contains("bimbo");
            bool t2Barcel = text2.Contains("barcel");

            if (t1Bimbo && !t1Barcel && !t2Bimbo && t2Barcel) return true;
            if (t1Barcel && !t1Bimbo && !t2Barcel && t2Bimbo) return true;

            return false;
        }

        private static double CalcularSimilitudJaccard(HashSet<string> set1, HashSet<string> set2)
        {
            if (set1.Count == 0 || set2.Count == 0) return 0;
            int interseccion = set1.Count(t => set2.Contains(t));
            int union = set1.Union(set2).Count();
            return (double)interseccion / union;
        }

        private static double CalcularSimilitudLevenshtein(string s, string t)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return 0;
            int bounds = Math.Max(s.Length, t.Length);
            int distance = LevenshteinDistance(s, t);
            return 1.0 - ((double)distance / bounds);
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}