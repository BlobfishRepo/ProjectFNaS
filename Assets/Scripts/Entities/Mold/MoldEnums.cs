namespace FNaS.Entities.Mold {
    public enum MoldSurfaceType {
        Ceiling,
        Wall,
        Floor,
        Light,
        Camera
    }

    public enum MoldSpreadState {
        Clean,
        Marked,
        Active,
        Isolated
    }

    public enum MoldCorruptionPhase {
        Normal,
        Blood
    }
}