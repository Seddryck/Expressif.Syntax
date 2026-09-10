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


test("tuple binding preserves named fields, token order, positions, and highlighting", () => {
  const parser = new Parser();
  parser.setLanguage(language);
  const source = "extend(\n ~subtract, unknown-function~\n)";
  const tree = parser.parse(source);
  assert.equal(tree.rootNode.hasError, false);
  const nodes = tree.rootNode.descendantsOfType("tuple_binding_shorthand");
  assert.deepEqual(nodes.map(node => ({
    text: node.text,
    name: node.childForFieldName("name").text,
    tilde: node.childForFieldName("tilde").text,
    children: node.children.map(child => child.type),
    named: node.namedChildren.map(child => child.type),
    start: node.startPosition,
    end: node.endPosition,
  })), [
    { text: "~subtract", name: "subtract", tilde: "~", children: ["~", "function_name"], named: ["function_name"], start: { row: 1, column: 1 }, end: { row: 1, column: 10 } },
    { text: "unknown-function~", name: "unknown-function", tilde: "~", children: ["function_name", "~"], named: ["function_name"], start: { row: 1, column: 12 }, end: { row: 1, column: 29 } },
  ]);
  const query = new Parser.Query(language, readFileSync(new URL("../../queries/highlights.scm", import.meta.url), "utf8"));
  assert.deepEqual(query.captures(tree.rootNode).filter(c => c.name === "operator").map(c => c.node.text), ["~", "~"]);
});

test("tuple binding errors recover to a following pipeline stage", () => {
  const parser = new Parser();
  parser.setLanguage(language);
  for (const source of ["subtract~~ | upper", "~~subtract | upper", "extend(~, 1) | upper"]) {
    const tree = parser.parse(source);
    assert.equal(tree.rootNode.hasError, true, source);
    assert.ok(tree.rootNode.descendantsOfType("function_call").some(node => node.text === "upper"), source);
  }
});


test("incremental edits invalidate tuple binding adjacency lookahead", () => {
  const parser = new Parser();
  parser.setLanguage(language);
  for (const initial of ["~subtract", "subtract~"]) {
    const index = initial.indexOf("~") === 0 ? 1 : 8;
    let tree = parser.parse(initial);
    const separated = initial.slice(0, index) + "/*gap*/" + initial.slice(index);
    tree.edit({ startIndex: index, oldEndIndex: index, newEndIndex: index + 7,
      startPosition: { row: 0, column: index }, oldEndPosition: { row: 0, column: index }, newEndPosition: { row: 0, column: index + 7 } });
    tree = parser.parse(separated, tree);
    assert.equal(tree.rootNode.hasError, true, separated);
    tree.edit({ startIndex: index, oldEndIndex: index + 7, newEndIndex: index,
      startPosition: { row: 0, column: index }, oldEndPosition: { row: 0, column: index + 7 }, newEndPosition: { row: 0, column: index } });
    tree = parser.parse(initial, tree);
    assert.equal(tree.rootNode.hasError, false, initial);
    assert.equal(tree.rootNode.descendantsOfType("tuple_binding_shorthand")[0].text, initial);
  }
});
