import assert from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import Parser from "tree-sitter";
import language from "./index.js";

test("can load grammar", () => {
  const parser = new Parser();
  assert.doesNotReject(async () => {
    const { default: language } = await import("./index.js");
    parser.setLanguage(language);
  });
});

test("from-end references preserve their direction tokens and highlighting", () => {
  const parser = new Parser();
  parser.setLanguage(language);
  const source = 'select($-1, $^0, "$-0") /* $-2 */';
  const tree = parser.parse(source);
  assert.equal(tree.rootNode.hasError, false);
  const projections = tree.rootNode.descendantsOfType("tuple_projection");
  assert.deepEqual(projections.map(node => ({
    text: node.text,
    start: node.startIndex,
    end: node.endIndex,
    direction: node.childForFieldName("direction").type,
    marker: node.childForFieldName("direction").text,
    index: node.childForFieldName("index").text,
  })), [
    { text: "$-1", start: 7, end: 10, direction: "from_end", marker: "-", index: "1" },
    { text: "$^0", start: 12, end: 15, direction: "from_end", marker: "^", index: "0" },
  ]);
  const query = new Parser.Query(language, readFileSync(new URL("../../queries/highlights.scm", import.meta.url), "utf8"));
  const captures = query.captures(tree.rootNode);
  assert.deepEqual(captures.filter(c => c.name === "operator").map(c => c.node.text), ["-", "^"]);
  assert.deepEqual(captures.filter(c => c.name === "number").map(c => c.node.text), ["1", "0"]);
});
