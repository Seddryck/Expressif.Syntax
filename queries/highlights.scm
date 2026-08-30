(function_name) @function

(numeric_literal) @number
(boolean_literal) @boolean
(type_literal) @type
(binary_operator) @operator
(unary_operator) @operator
(guarded_expression "*" @operator)
(escape_sequence) @string.escape

[
  (double_quoted_literal)
  (backtick_quoted_literal)
] @string

[
  (date_literal)
  (date_time_literal)
  (time_literal)
] @string.special

[
  "|"
  ","
] @punctuation.delimiter

[
  "("
  ")"
] @punctuation.bracket
