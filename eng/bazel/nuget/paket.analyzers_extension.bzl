"Generated"

load(":paket.analyzers.bzl", _analyzers = "analyzers")

def _analyzers_impl(module_ctx):
    _analyzers()
    return module_ctx.extension_metadata(reproducible = True)

analyzers_extension = module_extension(
    implementation = _analyzers_impl,
)
