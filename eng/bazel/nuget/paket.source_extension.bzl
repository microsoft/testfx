"Generated"

load(":paket.source.bzl", _source = "source")

def _source_impl(module_ctx):
    _source()
    return module_ctx.extension_metadata(reproducible = True)

source_extension = module_extension(
    implementation = _source_impl,
)
