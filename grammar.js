/**
 * @file Tree-sitter grammar for the Expressif expression language
 * @author Cédric L. Charlier <seddryck@gmail.com>
 * @license Apache-2.0
 */

/// <reference types="tree-sitter-cli/dsl" />
// @ts-check

export default grammar({
  name: "expressif",

  extras: ($) => [/[\s\uFEFF\u2060\u200B]/],

  supertypes: ($) => [
    $.value,
    $.quoted_literal,
    $.temporal_literal,
    $.expression,
  ],

  rules: {
    source_file: ($) => $.root_expression,

    root_expression: ($) => choice($.open_expression, $.closed_expression),

    open_expression: ($) => seq(
      $.expression,
      repeat(seq("|", $.expression)),
    ),

    closed_expression: ($) => seq(
      $.value,
      optional(seq("|", $.expression, repeat(seq("|", $.expression)))),
    ),

    expression: ($) => $.function_call,

    function_call: ($) => seq(
      field("name", $.function_name),
      optional(seq("(", optional($.argument_list), ")")),
    ),

    function_name: (_) => /[A-Za-z]+(?:-[A-Za-z]+)*/,

    argument_list: ($) => seq(
      $.positional_argument,
      repeat(seq(",", $.positional_argument)),
    ),

    positional_argument: ($) => $.value,

    value: ($) => choice(
      $.variable,
      $.property_reference,
      $.index_reference,
      $.numeric_literal,
      $.boolean_literal,
      $.quoted_literal,
      $.temporal_literal,
    ),

    numeric_literal: (_) => /-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?/,

    variable: (_) => /@[A-Za-z][A-Za-z0-9]*/,

    property_reference: (_) => /\[[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*\]/,

    index_reference: (_) => /\[(?:0|[1-9][0-9]*)\]/,

    boolean_literal: (_) => choice("#true", "#false"),

    quoted_literal: ($) => choice(
      $.double_quoted_literal,
      $.backtick_quoted_literal,
    ),

    // Quoted content is immediate and excludes CR/LF. This prevents global
    // whitespace extras from making multiline quoted literals valid.
    double_quoted_literal: ($) => seq(
      '"',
      repeat(choice($.double_quoted_content, $.escape_sequence)),
      '"',
    ),

    double_quoted_content: (_) => token.immediate(/[^"\\\r\n]+/),

    escape_sequence: (_) => token.immediate(/\\["\\]/),

    backtick_quoted_literal: ($) => seq(
      "`",
      optional($.backtick_quoted_content),
      "`",
    ),

    backtick_quoted_content: (_) => token.immediate(/[^`\r\n]+/),

    temporal_literal: ($) => choice(
      $.date_literal,
      $.date_time_literal,
      $.time_literal,
    ),

    date_literal: (_) => /#"[0-9]{4}-[0-9]{2}-[0-9]{2}"/,

    date_time_literal: (_) => /#"[0-9]{4}-[0-9]{2}-[0-9]{2}[T ][0-9]{2}:[0-9]{2}:[0-9]{2}"/,

    time_literal: (_) => /#"[0-9]{2}:[0-9]{2}:[0-9]{2}"/,
  },
});
