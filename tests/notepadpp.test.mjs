import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  generateNotepadPlusPlusUdl,
  loadGrammar,
} from "../scripts/generate-notepadpp.mjs";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const committedUdlPath = path.join(repositoryRoot, "editors", "notepadpp", "Expressif.xml");

function assertRepresentativeExpressifSyntax(xml) {
  assert.match(xml, /<UserLang name="Expressif" ext="expr" udlVersion="2\.1">/);
  assert.match(xml, /#true/);
  assert.match(xml, /#false/);
  assert.match(xml, /\|&gt;/);
  assert.match(xml, /:=/);
  assert.match(xml, /\.\.\./);
  assert.match(xml, /<Keywords name="Keywords2">[^<]*\.[^<]*@[^<]*<\/Keywords>/);
  assert.match(xml, /00#&quot;.*03&quot;.*06`.*08`/);
}

test("Notepad++ UDL generation is deterministic", () => {
  const grammar = loadGrammar();
  const first = generateNotepadPlusPlusUdl(grammar);
  const second = generateNotepadPlusPlusUdl(grammar);

  assert.equal(first, second);
  assertRepresentativeExpressifSyntax(first);
});

test("committed Notepad++ UDL covers representative Expressif syntax", () => {
  const xml = fs.readFileSync(committedUdlPath, "utf8");
  assertRepresentativeExpressifSyntax(xml);
});
