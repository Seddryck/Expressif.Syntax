import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const defaultGrammarPath = path.join(repositoryRoot, "src", "grammar.json");
const defaultOutputPath = path.join(repositoryRoot, "editors", "notepadpp", "Expressif.xml");

function collectNodes(node, predicate, values = []) {
  if (!node || typeof node !== "object") {
    return values;
  }

  if (predicate(node)) {
    values.push(node);
  }

  for (const value of Object.values(node)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        collectNodes(item, predicate, values);
      }
    } else if (value && typeof value === "object") {
      collectNodes(value, predicate, values);
    }
  }

  return values;
}

function terminalStrings(rule) {
  return [...new Set(
    collectNodes(rule, (node) => node.type === "STRING" && typeof node.value === "string")
      .map((node) => node.value),
  )];
}

function literalPrefixBeforeCharacterClass(pattern) {
  let prefix = "";
  let index = 0;

  while (index < pattern.length) {
    const character = pattern[index];

    if (character === "[") {
      return prefix && !prefix.includes('"') && !prefix.includes("\\") ? prefix : "";
    }

    if (character === "\\" && index + 1 < pattern.length) {
      const escaped = pattern[index + 1];
      if (/[^A-Za-z0-9]/.test(escaped)) {
        prefix += escaped;
        index += 2;
        continue;
      }
      return "";
    }

    if (/[^A-Za-z0-9(){}?*+|.^$]/.test(character)) {
      prefix += character;
      index += 1;
      continue;
    }

    return "";
  }

  return "";
}

function lexicalPrefixes(grammar) {
  const patterns = collectNodes(grammar.rules, (node) => node.type === "PATTERN" && typeof node.value === "string")
    .map((node) => literalPrefixBeforeCharacterClass(node.value))
    .filter(Boolean);

  return [...new Set(patterns)].sort((left, right) => right.length - left.length || left.localeCompare(right));
}

function xmlEscape(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&apos;");
}

function keywordValues(grammar) {
  const lexicalMarkers = new Set(["\"", "`"]);
  const values = terminalStrings(grammar.rules)
    .filter((value) => /[A-Za-z]/.test(value))
    .filter((value) => !lexicalMarkers.has(value));

  return [...new Set(values)].sort((left, right) => left.localeCompare(right));
}

function operatorValues(grammar) {
  const excluded = new Set(["\"", "`"]);
  return terminalStrings(grammar.rules)
    .filter((value) => !/[A-Za-z0-9]/.test(value))
    .filter((value) => !excluded.has(value))
    .sort((left, right) => right.length - left.length || left.localeCompare(right));
}

function formatKeywordList(values) {
  return values.map(xmlEscape).join(" ");
}

export function generateNotepadPlusPlusUdl(grammar) {
  const keywords = keywordValues(grammar);
  const prefixes = lexicalPrefixes(grammar);
  const operators = operatorValues(grammar);

  return `<?xml version="1.0" encoding="Windows-1252" ?>
<NotepadPlus>
    <UserLang name="Expressif" ext="expr" udlVersion="2.1">
        <Settings>
            <Global caseIgnored="no" allowFoldOfComments="yes" foldCompact="no" forcePureLC="0" decimalSeparator="0" />
            <Prefix Keywords1="no" Keywords2="yes" Keywords3="no" Keywords4="no" Keywords5="no" Keywords6="no" Keywords7="no" Keywords8="no" />
        </Settings>
        <KeywordLists>
            <Keywords name="Comments">00 01 02 03</Keywords>
            <Keywords name="Numbers, prefix1"></Keywords>
            <Keywords name="Numbers, prefix2"></Keywords>
            <Keywords name="Numbers, extras1"></Keywords>
            <Keywords name="Numbers, extras2"></Keywords>
            <Keywords name="Numbers, suffix1"></Keywords>
            <Keywords name="Numbers, suffix2"></Keywords>
            <Keywords name="Numbers, range"></Keywords>
            <Keywords name="Operators1">${formatKeywordList(operators)}</Keywords>
            <Keywords name="Operators2"></Keywords>
            <Keywords name="Folders in code1, open"></Keywords>
            <Keywords name="Folders in code1, middle"></Keywords>
            <Keywords name="Folders in code1, close"></Keywords>
            <Keywords name="Folders in code2, open"></Keywords>
            <Keywords name="Folders in code2, middle"></Keywords>
            <Keywords name="Folders in code2, close"></Keywords>
            <Keywords name="Folders in comment, open"></Keywords>
            <Keywords name="Folders in comment, middle"></Keywords>
            <Keywords name="Folders in comment, close"></Keywords>
            <Keywords name="Keywords1">${formatKeywordList(keywords)}</Keywords>
            <Keywords name="Keywords2">${formatKeywordList(prefixes)}</Keywords>
            <Keywords name="Keywords3"></Keywords>
            <Keywords name="Keywords4"></Keywords>
            <Keywords name="Keywords5"></Keywords>
            <Keywords name="Keywords6"></Keywords>
            <Keywords name="Keywords7"></Keywords>
            <Keywords name="Keywords8"></Keywords>
            <Keywords name="Delimiters">00#&quot; 01 02&quot; 03&quot; 04\\ 05&quot; 06` 07 08` 09 10 11 12 13 14 15 16 17 18 19 20 21 22 23</Keywords>
        </KeywordLists>
        <Styles>
            <WordsStyle name="DEFAULT" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="COMMENTS" fgColor="008000" bgColor="FFFFFF" fontName="" fontStyle="2" nesting="0" />
            <WordsStyle name="LINE COMMENTS" fgColor="008000" bgColor="FFFFFF" fontName="" fontStyle="2" nesting="0" />
            <WordsStyle name="NUMBERS" fgColor="FF8000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS1" fgColor="0000FF" bgColor="FFFFFF" fontName="" fontStyle="1" nesting="0" />
            <WordsStyle name="KEYWORDS2" fgColor="800080" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS3" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS4" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS5" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS6" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS7" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="KEYWORDS8" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="OPERATORS" fgColor="000080" bgColor="FFFFFF" fontName="" fontStyle="1" nesting="0" />
            <WordsStyle name="FOLDER IN CODE1" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="FOLDER IN CODE2" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="FOLDER IN COMMENT" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS1" fgColor="008080" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS2" fgColor="A31515" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS3" fgColor="A31515" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS4" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS5" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS6" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS7" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
            <WordsStyle name="DELIMITERS8" fgColor="000000" bgColor="FFFFFF" fontName="" fontStyle="0" nesting="0" />
        </Styles>
    </UserLang>
</NotepadPlus>
`;
}

export function loadGrammar(grammarPath = defaultGrammarPath) {
  return JSON.parse(fs.readFileSync(grammarPath, "utf8"));
}

export function writeNotepadPlusPlusUdl(outputPath = defaultOutputPath, grammarPath = defaultGrammarPath) {
  const grammar = loadGrammar(grammarPath);
  const xml = generateNotepadPlusPlusUdl(grammar);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, xml, "utf8");
  return xml;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  writeNotepadPlusPlusUdl();
}
