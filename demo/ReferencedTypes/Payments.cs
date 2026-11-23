[assembly: Fprog.FunctionalCore]

namespace Demo.Fp0502_SwitchStatementMissesCase;

public closed record Payment;
public sealed record Card : Payment;
public sealed record Cash : Payment;
public sealed record Voucher : Payment;
