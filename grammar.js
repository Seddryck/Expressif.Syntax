/**
 * @file Tree-sitter grammar for the Expressif expression language
 * @author Cédric L. Charlier <seddryck@gmail.com>
 * @license Apache-2.0
 */

/// <reference types="tree-sitter-cli/dsl" />
// @ts-check

export default grammar({
  name: "expressif",

  rules: {
    source_file: ($) => repeat($.expression),

    // Initial placeholder rule. The complete Expressif syntax is introduced
    // incrementally together with corpus tests.
    expression: (_) => "hello",
  },
});
