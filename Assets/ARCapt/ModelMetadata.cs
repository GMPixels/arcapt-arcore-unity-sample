using System;

namespace Assets.ARCapt
{
    [Serializable]
    public class ModelMetadata
    {
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double RotX { get; set; }
        public double RotY { get; set; }
        public double RotZ { get; set; }
        public double ScaX { get; set; }
        public double ScaY { get; set; }
        public double ScaZ { get; set; }

        public ModelMetadata()
        {
            this.PosX = 0;
            this.PosY = 0;
            this.PosZ = 0;

            this.RotX = 0;
            this.RotY = 0;
            this.RotZ = 0;

            this.ScaX = 1;
            this.ScaY = 1;
            this.ScaZ = 1;
        }
    }
}
