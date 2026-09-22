#include "tree_sitter/parser.h"

enum TokenType { BINDING_PREFIX_TILDE, BINDING_POSTFIX_NAME, TAGGED_RECORD_NAME, BOOLEAN_OPERATOR };

static bool is_letter(int32_t c) {
  return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}

static bool is_name_character(int32_t c) {
  return is_letter(c) || (c >= '0' && c <= '9') || c == '_' || c == '-';
}

static char upper_ascii(int32_t c) {
  return c >= 'a' && c <= 'z' ? (char)(c - ('a' - 'A')) : (char)c;
}

static bool is_boolean_operator(const char *text, unsigned length) {
  if (length == 2) return text[0] == 'O' && text[1] == 'R';
  if (length == 3)
    return (text[0] == 'A' && text[1] == 'N' && text[2] == 'D') ||
           (text[0] == 'X' && text[1] == 'O' && text[2] == 'R') ||
           (text[0] == 'N' && text[1] == 'O' && text[2] == 'R');
  if (length == 4)
    return text[0] == 'N' &&
           ((text[1] == 'A' && text[2] == 'N' && text[3] == 'D') ||
            (text[1] == 'X' && text[2] == 'O' && text[3] == 'R'));
  return false;
}

// Match the grammar's whitespace extras (ECMAScript whitespace plus zero-width extras).
static bool is_space(int32_t c) {
  return (c >= 9 && c <= 13) || c == 32 || c == 0xa0 || c == 0x1680 ||
         (c >= 0x2000 && c <= 0x200b) || c == 0x2028 || c == 0x2029 ||
         c == 0x202f || c == 0x205f || c == 0x2060 || c == 0x3000 || c == 0xfeff;
}

void *tree_sitter_expressif_external_scanner_create(void) { return NULL; }
void tree_sitter_expressif_external_scanner_destroy(void *payload) { (void)payload; }
unsigned tree_sitter_expressif_external_scanner_serialize(void *payload, char *buffer) {
  (void)payload;
  (void)buffer;
  return 0;
}
void tree_sitter_expressif_external_scanner_deserialize(void *payload, const char *buffer, unsigned length) {
  (void)payload;
  (void)buffer;
  (void)length;
}

bool tree_sitter_expressif_external_scanner_scan(void *payload, TSLexer *lexer, const bool *valid_symbols) {
  (void)payload;
  while (is_space(lexer->lookahead)) lexer->advance(lexer, true);

  if (valid_symbols[BOOLEAN_OPERATOR] && lexer->lookahead == '|') {
    char text[5];
    unsigned length = 0;
    lexer->advance(lexer, false);
    while (is_letter(lexer->lookahead) && length < 5) {
      text[length++] = upper_ascii(lexer->lookahead);
      lexer->advance(lexer, false);
    }
    if (length > 4 || is_name_character(lexer->lookahead) || !is_boolean_operator(text, length))
      return false;
    lexer->mark_end(lexer);
    lexer->result_symbol = BOOLEAN_OPERATOR;
    return true;
  }

  // Only emit the prefix when the name immediately follows it. Named comment
  // extras otherwise bypass token.immediate and would allow ~/*comment*/f.
  if (valid_symbols[BINDING_PREFIX_TILDE] && lexer->lookahead == '~') {
    lexer->advance(lexer, false);
    lexer->mark_end(lexer);
    if (!is_letter(lexer->lookahead)) return false;
    lexer->result_symbol = BINDING_PREFIX_TILDE;
    return true;
  }

  // Look ahead to the adjacent discriminator without consuming it, preserving
  // separate name and punctuation nodes while excluding comments and whitespace.
  if ((valid_symbols[BINDING_POSTFIX_NAME] || valid_symbols[TAGGED_RECORD_NAME]) &&
      is_letter(lexer->lookahead)) {
    do {
      while (is_letter(lexer->lookahead)) lexer->advance(lexer, false);
      if (lexer->lookahead != '-') break;
      lexer->advance(lexer, false);
      if (!is_letter(lexer->lookahead)) return false;
    } while (true);
    lexer->mark_end(lexer);
    if (valid_symbols[BINDING_POSTFIX_NAME] && lexer->lookahead == '~') {
      lexer->result_symbol = BINDING_POSTFIX_NAME;
      return true;
    }
    if (valid_symbols[TAGGED_RECORD_NAME] && lexer->lookahead == '{') {
      lexer->result_symbol = TAGGED_RECORD_NAME;
      return true;
    }
  }
  return false;
}
