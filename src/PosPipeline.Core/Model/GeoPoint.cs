namespace PosPipeline.Core.Model;

/// <summary>A position in decimal degrees. Negative latitude is south, negative longitude is west.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);
