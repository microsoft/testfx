# MSTEST0001

| Property                            | Value                                               |
|-------------------------------------|-----------------------------------------------------|
| **Rule ID**                         | MSTEST0001                                          |
| **Title**                           | Explicitly enable or disable tests parallelization. |
| **Category**                        | [Performance](performance-warnings.md)              |
| **Enabled by default**    | Yes                                                 |

## Cause

The assembly is not marked with `[Parallelize]` or `[DoNotParallelize]` attribute.

## Rule description

By default, MSTest runs tests sequentially which can lead to severe performance limitations. It is recommended to enable assembly attribute `[Parallelize]` or if the assembly is known to not be parallelizable, to use explicitly the assembly level attribute `[DoNotParallelize]`.

## How to fix violations

Add `[assembly: Parallelize]` or `[assembly: DoNotParallelize]` attribute.
