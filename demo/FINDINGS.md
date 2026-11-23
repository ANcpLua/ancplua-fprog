# Known gaps

| File | Bug that passes | Reason |
| --- | --- | --- |
| `Fp0101_VoidDelegates.cs` | Core accepts, invokes and returns a void delegate. | FP0101 currently checks void declarations on methods and local functions. |
| `Fp0601_CastThroughObject.cs` | A cast and a type test recover runtime type information from an `object`. | FP0601 still focuses on type parameters. |
