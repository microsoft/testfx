"Generated"

load(":paket.generator.bzl", _generator = "generator")

def _generator_impl(module_ctx):
    _generator()
    return module_ctx.extension_metadata(reproducible = True)

generator_extension = module_extension(
    implementation = _generator_impl,
)
