(function_name) @function

(numeric_literal) @number
(boolean_literal) @boolean
(type_literal) @type
(binary_operator) @operator
(unary_operator) @operator
(guarded_expression "*" @operator)
(expression_root) @variable.builtin
(pair_literal "=>" @operator)
(grouping_literal "#{" @punctuation.bracket)
(dictionary_literal "!{" @punctuation.bracket)
(escape_sequence) @string.escape

[
  (line_comment)
  (block_comment)
] @comment

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
