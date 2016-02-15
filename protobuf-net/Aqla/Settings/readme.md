These classes are internal holders for settings. 
They are shared through the project and should not contain anything specific to any client like DerivedTypes so clients are not forced to maintain them.
They should be easily copied so struct.



//They should not have any logic and all nested data should be easily accessible (with value types you can't directly set property on nested value) so just public fields
Nope, this way you can't delegate anything to internal structs...