namespace Mantenimientos.Models.ViewModels
{
    public class ImportResumenVM
    {
        public int TotalFilas { get; set; }
        public int Actualizados { get; set; }
        public int Imprecisos { get; set; }
        public int NoEncontrados { get; set; }

        public List<string> NombresActualizados { get; set; } = new();
        public List<string> NombresImprecisos { get; set; } = new();
        public List<string> NombresNoEncontrados { get; set; } = new();
    }
}
