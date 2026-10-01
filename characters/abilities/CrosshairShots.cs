public enum CrosshairShotStyle { FlankingArcs, Row }

public readonly record struct CrosshairShots(CrosshairShotStyle Style, int Remaining, int Total);
