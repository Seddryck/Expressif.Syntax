# Notepad++ syntax highlighting

`Expressif.xml` is a generated Notepad++ User Defined Language (UDL) definition for Expressif `.expr` files.

The canonical grammar remains `grammar.js`. The export pipeline is:

```text
grammar.js
    ↓ tree-sitter generate
src/grammar.json
    ↓ scripts/generate-notepadpp.mjs
editors/notepadpp/Expressif.xml
```

This keeps the Notepad++ highlighting definition derived from the same grammar used by the parser instead of maintaining a separate list of Expressif tokens.

## Regenerate

Run:

```sh
npm run generate
```

This regenerates both the Tree-sitter parser artifacts and the Notepad++ UDL. To regenerate only the Notepad++ artifact from the current `src/grammar.json`, run:

```sh
npm run generate:notepadpp
```

## Import into Notepad++

1. Open **Language → User Defined Language → Define your language...**.
2. Choose **Import...**.
3. Select `editors/notepadpp/Expressif.xml`.
4. Restart Notepad++ if the imported language is not immediately available.

The UDL is a syntax-highlighting projection only. Notepad++ UDL cannot represent the complete Tree-sitter grammar or parser semantics, so `grammar.js` remains authoritative for parsing and validation.
