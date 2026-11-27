namespace Demo.Fp0502_MostDerivedConstraint;

public closed record ShipmentEvent;
public sealed record Created : ShipmentEvent;
public closed record TransitEvent : ShipmentEvent;
public sealed record Arrived : TransitEvent;
public sealed record Departed : TransitEvent;
