/**
 * @file Tree-sitter grammar for the Expressif expression language
 * @author Cédric L. Charlier <seddryck@gmail.com>
 * @license Apache-2.0
 */

/// <reference types="tree-sitter-cli/dsl" />
// @ts-check

const ordinaryExpression = ($) => choice(
  $.function_call,
  $.map_shorthand,
  $.tuple_projection,
  prec(-1, $.pair_component_access),
  $.parenthesized_expression,
);

const privateName = /_[A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/;
const unquotedPublicName = /[A-Za-z][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/;

export default grammar({
  name: "expressif",

  extras: ($) => [/[\s\uFEFF\u2060\u200B]/],

  supertypes: ($) => [
    $.value,
    $.quoted_literal,
    $.temporal_literal,
    $.expression,
  ],

  conflicts: ($) => [
    [$.array_element, $.record_spread],
    [$.value, $._compound_value],
    [$.expression, $._pipeline_expression],
    [$.expression, $._shorthand_operand],
    [$._map_shorthand_expression, $.value],
    [$.root_expression, $._parenthesized_pipeline_expression],
  ],

  rules: {
    source_file: ($) => $.root_expression,

    root_expression: ($) => choice(
      $.open_expression,
      $.closed_expression,
    ),

    open_expression: ($) => prec.right(seq(
      $.expression,
      repeat(choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      )),
    )),

    closed_expression: ($) => seq(
      $.value,
      repeat(choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      )),
    ),

    map_shorthand: ($) => seq(
      "|>",
      field("expression", alias($._map_shorthand_expression, $.open_expression)),
    ),

    _map_shorthand_expression: ($) => choice(
      $.expression,
      $.record_access,
    ),

    // In a closed-expression pipeline, the next ordinary `|` belongs to the
    // outer pipeline. Alias the single mapped operation as an open expression
    // so both forms expose the same CST shape.
    _pipeline_map_shorthand: ($) => choice(
      prec(1, seq(
        "|>",
        field("expression", alias($.parenthesized_open_expression, $.open_expression)),
      )),
      seq(
        "|>",
        field("expression", alias($._map_shorthand_expression, $.open_expression)),
      ),
    ),

    expression: ($) => choice(
      $.binary_expression,
      $.guarded_expression,
      $.unary_expression,
      ordinaryExpression($),
    ),

    binary_expression: ($) => prec.left(1, seq(
      field("left", choice($.binary_expression, $._shorthand_operand)),
      field("operator", $.binary_operator),
      field("right", $._shorthand_operand),
    )),

    binary_operator: (_) => choice(
      "->",
      "|AND", "|OR", "|XOR",
      "|NAND", "|NOR", "|NXOR",
    ),

    unary_expression: ($) => prec.right(2, seq(
      field("operator", $.unary_operator),
      field("operand", choice($.unary_expression, ordinaryExpression($), $.value)),
    )),

    unary_operator: (_) => "!",

    guarded_expression: ($) => prec.right(2, seq(
      "*",
      field("expression", choice(
        $.guarded_expression,
        $.unary_expression,
        ordinaryExpression($),
        $.value,
      )),
    )),

    _shorthand_operand: ($) => choice(
      $.guarded_expression,
      $.unary_expression,
      ordinaryExpression($),
      $.value,
    ),

    _pipeline_expression: ($) => choice(
      $.binary_expression,
      $.guarded_expression,
      $.unary_expression,
      $.function_call,
      prec(1, $.record_access),
      $.tuple_projection,
      $.pair_component_access,
      alias($._parenthesized_pipeline_expression, $.parenthesized_expression),
    ),

    _parenthesized_pipeline_expression: ($) => seq(
      "(",
      field("expression", choice(
        alias($._pipeline_open_expression, $.open_expression),
        $.closed_expression,
      )),
      ")",
    ),

    _pipeline_open_expression: ($) => prec.right(seq(
      $._pipeline_expression,
      repeat(seq("|", $._pipeline_expression)),
    )),

    parenthesized_expression: ($) => seq(
      "(",
      field("expression", $.root_expression),
      ")",
    ),

    // Preserve the open-expression shape expected by map shorthand while
    // retaining the parenthesized expression as its single operation.
    parenthesized_open_expression: ($) => prec(1, $.parenthesized_expression),

    tuple_projection: ($) => choice(
      seq(
        field("direction", alias("$", $.from_start)),
        field("index", alias(token.immediate(prec(-1, /(?:0|[1-9][0-9]*)/)), $.tuple_index)),
      ),
      seq(
        "$",
        field("direction", alias(token.immediate("^"), $.from_end)),
        field("index", alias(token.immediate(prec(-1, /(?:0|[1-9][0-9]*)/)), $.tuple_index)),
      ),
    ),

    pair_component_access: (_) => /\$(?:key|value)/,

    function_call: ($) => seq(
      field("name", alias($._function_name, $.function_name)),
      optional(seq(
        "(",
        optional(choice(
          $.argument_list,
          alias($._trailing_argument_list, $.argument_list),
        )),
        ")",
      )),
    ),

    _function_name: (_) => /[A-Za-z]+(?:-[A-Za-z]+)*/,

    argument_list: ($) => prec.left(seq(
      choice($.positional_argument, $.named_argument, $.spread_argument),
      repeat(seq(",", choice($.positional_argument, $.named_argument, $.spread_argument))),
    )),

    _trailing_argument_list: ($) => prec.right(seq(
      choice($.positional_argument, $.named_argument, $.spread_argument),
      repeat(seq(",", choice($.positional_argument, $.named_argument, $.spread_argument))),
      ",",
    )),

    positional_argument: ($) => $._argument_value,

    spread_argument: ($) => seq(
      "...",
      field("value", optional($._spread_operand)),
    ),

    _spread_operand: ($) => choice(
      alias($._nested_closed_expression, $.closed_expression),
      $.value,
      $.tuple_projection,
      $.pair_component_access,
      $.parameterized_expression,
      $.parenthesized_expression,
      $.guarded_expression,
      $.unary_expression,
      $.binary_expression,
    ),

    named_argument: ($) => seq(
      field("name", $.argument_name),
      ":=",
      field("value", $._argument_value),
    ),

    argument_name: ($) => choice(
      $.public_argument_name,
      $.private_argument_name,
    ),

    private_argument_name: (_) => privateName,

    public_argument_name: ($) => choice(
      alias($._function_name, $.unquoted_argument_name),
      $.unquoted_argument_name,
      $.double_quoted_literal,
      $.backtick_quoted_literal,
    ),

    unquoted_argument_name: (_) => unquotedPublicName,

    _argument_value: ($) => choice(
      $.binary_expression,
      $.guarded_expression,
      $.unary_expression,
      alias($._nested_closed_expression, $.closed_expression),
      $.value,
      $.tuple_projection,
      $.pair_component_access,
      alias($._nested_open_expression, $.open_expression),
      $.parameterized_expression,
      $.parenthesized_expression,
    ),

    // Parentheses delimit the nested expression, so a function call or
    // function pipeline can be passed directly as a higher-order argument.
    _nested_open_expression: ($) => choice(
      seq(
        $.function_call,
        repeat(seq("|", $._pipeline_expression)),
      ),
      seq(
        $.tuple_projection,
        "|",
        $._pipeline_expression,
        repeat(seq("|", $._pipeline_expression)),
      ),
      seq(
        $.pair_component_access,
        "|",
        $._pipeline_expression,
        repeat(seq("|", $._pipeline_expression)),
      ),
    ),

    // A positional argument is already delimited by its function call's
    // parentheses, so a closed pipeline does not need additional braces.
    // Require at least one pipeline operation here to keep literal arguments
    // unambiguous with the ordinary value alternative above.
    _nested_closed_expression: ($) => seq(
      $.value,
      choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      ),
      repeat(choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      )),
    ),

    parameterized_expression: ($) => prec.dynamic(3, seq(
      "{",
      field("source", choice($.value, $.tuple_projection)),
      "|",
      field("expression", $.open_expression),
      "}",
    )),

    value: ($) => choice(
      $.incoming_value,
      $.constant_reference,
      $.variable,
      $.type_literal,
      $.record_access,
      $.numeric_literal,
      $.boolean_literal,
      $.null_literal,
      $.quoted_literal,
      $.temporal_literal,
      $.array_literal,
      $.tuple_literal,
      $.pair_literal,
      $.record_literal,
      $.interval_literal,
    ),

    interval_literal: ($) => seq(
      "I",
      choice(
        seq(
          field("lower_delimiter", choice("[", "(", "]")),
          field("lower_bound", $.interval_bound),
          ",",
          field("upper_bound", $.interval_bound),
          field("upper_delimiter", choice("]", ")", "[")),
        ),
        $.interval_shorthand,
      ),
    ),

    interval_bound: ($) => choice(
      $.numeric_literal,
      $.boolean_literal,
      $.quoted_literal,
      $.temporal_literal,
      $.infinite_bound,
    ),

    infinite_bound: (_) => choice("+INF", "-INF"),

    interval_shorthand: (_) => choice(
      "(0+)", "(+)", "(0-)", "(-)",
      "(positive)", "(negative)",
      "(absolutely-positive)", "(absolutely-negative)",
      /\((?:>=|<=|>|<)-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?\)/,
    ),

    array_literal: ($) => prec.dynamic(1, choice(
      seq("{", "}"),
      prec(1, seq(
        "{",
        $.array_element,
        repeat(seq(",", $.array_element)),
        "}",
      )),
      seq(
        "{",
        $.array_element,
        repeat(seq(",", $.array_element)),
        "}",
      ),
    )),

    array_element: ($) => choice(
      seq(
        field("spread", "..."),
        field("expression", optional($._positional_spread_operand)),
      ),
      field("expression", choice(
        alias($._array_closed_expression, $.closed_expression),
        $._compound_value,
      )),
    ),

    _array_closed_expression: ($) => prec.right(2, seq(
      $._compound_value,
      choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      ),
      repeat(choice(
        seq("|", $._pipeline_expression),
        alias($._pipeline_map_shorthand, $.map_shorthand),
      )),
    )),

    _positional_spread_operand: ($) => choice(
      alias($._array_closed_expression, $.closed_expression),
      $._compound_value,
      $.parenthesized_expression,
      $.guarded_expression,
      $.unary_expression,
      $.binary_expression,
    ),

    _compound_value: ($) => choice(
      $.incoming_value,
      $.constant_reference,
      $.variable,
      $.type_literal,
      $.record_access,
      $.numeric_literal,
      $.boolean_literal,
      $.null_literal,
      $.quoted_literal,
      $.temporal_literal,
      $.array_literal,
      $.tuple_literal,
      $.pair_literal,
      $.record_literal,
      $.interval_literal,
    ),

    tuple_literal: ($) => seq(
      "T",
      "(",
      choice(
        seq(
          alias($.tuple_leading_spread_element, $.tuple_element),
          repeat(seq(",", $.tuple_element)),
        ),
        seq(
          $.tuple_element,
          ",",
          $.tuple_element,
          repeat(seq(",", $.tuple_element)),
        ),
      ),
      ")",
    ),

    tuple_element: ($) => choice(
      seq(
        field("spread", "..."),
        field("expression", optional($._positional_spread_operand)),
      ),
      field("expression", choice(
        alias($._array_closed_expression, $.closed_expression),
        $._compound_value,
      )),
    ),

    tuple_leading_spread_element: ($) => prec(1, seq(
      field("spread", "..."),
      field("expression", optional($._positional_spread_operand)),
    )),

    pair_literal: ($) => seq(
      "(",
      field("key", $.root_expression),
      "=>",
      field("value", $.root_expression),
      ")",
    ),

    record_literal: ($) => choice(
      seq("{", ":", "}"),
      prec.dynamic(2, seq(
        "{",
        $._record_entry,
        repeat(seq(",", $._record_entry)),
        "}",
      )),
    ),

    _record_entry: ($) => choice(
      $.record_field,
      $.record_spread,
    ),

    record_field: ($) => seq(
      field("name", $.record_field_name),
      ":=",
      choice(
        seq(
          field("spread", "..."),
          field("value", optional(choice($._compound_value, $.expression))),
        ),
        field("value", $._compound_value),
      ),
    ),

    record_spread: (_) => "...",

    incoming_value: (_) => "@_",

    constant_reference: (_) => /@![A-Za-z][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/,

    record_field_name: ($) => choice(
      $.private_record_field_name,
      $.public_record_field_name,
    ),

    private_record_field_name: (_) => privateName,

    public_record_field_name: ($) => choice(
      $.unquoted_record_field_name,
      $.double_quoted_literal,
      $.backtick_quoted_literal,
    ),

    unquoted_record_field_name: (_) => unquotedPublicName,

    numeric_literal: (_) => /-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?/,

    variable: (_) => /@[A-Za-z][A-Za-z0-9]*/,

    record_access: ($) => seq(
      choice(
        field("field", $.record_field_selector),
        seq(
          field("root", $.original_input),
          field("field", $.original_record_field_selector),
        ),
      ),
      repeat(field("field", $.immediate_record_field_selector)),
    ),

    original_input: (_) => /\^\./,

    record_field_selector: ($) => choice(
      $.named_record_field,
      $.positional_record_field,
    ),

    immediate_record_field_selector: ($) => choice(
      alias(token.immediate(/\.[A-Za-z_][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/), $.named_record_field),
      alias(token.immediate(/\.(?:0|[1-9][0-9]*)/), $.positional_record_field),
    ),

    original_record_field_selector: ($) => choice(
      alias(token.immediate(prec(-1, /[A-Za-z_][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/)), $.named_record_field),
      alias(token.immediate(prec(-1, /(?:0|[1-9][0-9]*)/)), $.positional_record_field),
    ),

    named_record_field: (_) => /\.[A-Za-z_][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*/,

    positional_record_field: (_) => /\.(?:0|[1-9][0-9]*)/,

    boolean_literal: (_) => choice("#true", "#false"),

    type_literal: (_) => token(seq(":", /[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*/)),

    null_literal: (_) => "#null",

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
