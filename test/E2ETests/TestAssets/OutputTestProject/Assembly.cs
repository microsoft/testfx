using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]
[assembly: ClassCleanupExecution(ClassCleanupBehavior.EndOfClass)]
