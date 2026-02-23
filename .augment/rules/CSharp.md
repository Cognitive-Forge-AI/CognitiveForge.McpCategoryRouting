---
type: "agent_requested"
description: ".NET and CSharp (C#) code guidelines (*.cs)"
---

# General

-   Favor primary constructor whenever possible, especially on record classes.
-   Favor dependency injection over creating classes using the `new` keyword unless the class is a pure data structure like a DTO.
-   Favor Integration Tests that are as close as end-to-end as possible over mocks-heavy unit tests. Use unit tests for code that contains algorithms with many code paths, that are easy to test in isolation, that testing in isolation brings value and confidence, and that keep the test suite easy to maintain. Do not test code, test "business logic" and rules.
-   When using the Options pattern, favor injecting options directly, like `DailyLinkSettings settings` instead of `IOptions<DailyLinkSettings>`.
-   Favor the early return pattern over nested if/else statements.
-   Favor result objects over success=true/false.
-   Favor an `enum` over a "magic string" or instead of a short list of strings.

## Dependency Injection

-   Use dependency injection (DI) with proper inversion of control (IoC).
-   Use constructor injection, if impossible use method injection.
-   Use Singleton lifetime for stateless objects, if you can't, fallback to Scoped, finally if you can't, fallback to Transient, unless a lifetime is more optimal for the specific case.
-   Always validate constructor-injected dependencies with null guards using `?? throw new ArgumentNullException(nameof(parameter))` to fail fast if DI misconfiguration supplies null.
