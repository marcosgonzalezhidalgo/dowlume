# Domain-model diagrams

The [domain model](../../DOMAIN_MODEL.md) explains these ten manually authored UML views. `src/` contains their PlantUML sources and the shared `style.iuml`; `svg/` contains generated vector images embedded directly in Markdown. Edit the sources, then regenerate the SVGs. Do not edit generated SVGs by hand.

## Reproduce the images

Validated toolchain: **PlantUML 1.2026.8**, **Eclipse Temurin Java 21.0.12.1**, Windows x64. The class, object, and state diagrams request the embedded [Smetana layout engine](https://plantuml.com/smetana02); no separate Graphviz installation is required. Different renderer versions or installed fonts can change layout. Diagrams request Arial and use a white background for readable GitHub rendering.

Download the JAR from the [PlantUML 1.2026.8 release](https://github.com/plantuml/plantuml/releases/tag/v1.2026.8) and keep it outside versioned documentation, for example under `artifacts/tools/domain-model/plantuml.jar`. Its SHA-256 is:

```text
5e1ecfa8ecd32c90b03bbf3b1eb6f020943f98ab0fcf4032be31a0002ee2c462
```

From the repository root with Java available:

```powershell
Get-FileHash artifacts/tools/domain-model/plantuml.jar -Algorithm SHA256
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Render-DomainModel.ps1 -PlantUmlJar artifacts/tools/domain-model/plantuml.jar -PreviewPng
```

Compare the reported hash with the value above. If Java is not on `PATH`, pass `-JavaPath "C:\path\to\java.exe"`. The script uses PlantUML's [command-line renderer](https://plantuml.com/command-line): it checks syntax before rendering, fails on rendering errors, and verifies each output is SVG XML without embedded scripts or HTML. No diagrams are uploaded to a rendering service. The optional PNG previews are written to ignored `artifacts/domain-model-preview/`; the SVGs remain under `svg/`.

## Review before committing

1. Inspect every image at full size and at Markdown reading width. Follow the diagram and check labels, arrowheads, multiplicities, branches, and clipped or overlapping text. Use the full SVG link for larger views.
2. Check each diagram against the vocabulary and rules in `DOMAIN_MODEL.md`. Syntax validation cannot prove domain correctness.
3. Confirm that the object snapshot satisfies the class model's constraints; omitted objects must be declared as omissions.
4. Confirm that alternative sequence/activity outcomes agree with both state machines, including failed and unresolved attempts.
5. Check local Markdown image/source links and `git diff --check`. Commit the matching `.puml`, `.iuml`, and `.svg` changes together.

The scope is analysis documentation. The rendering tool does not build or run Dowlume and does not alter application data. Tool binaries and preview PNGs are not committed. Dated evidence belongs in [Validation](../../../VALIDATION.md).
