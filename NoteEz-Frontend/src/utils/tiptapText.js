// Flattens TipTap document JSON into plain text, e.g. for card previews.
export function tiptapToPlainText(content) {
  if (!content) return ''

  let doc = content
  if (typeof content === 'string') {
    try {
      doc = JSON.parse(content)
    } catch {
      return content
    }
  }

  const parts = []
  walk(doc, parts)
  return parts.join('').trim()
}

function walk(node, parts) {
  if (!node || typeof node !== 'object') return

  if (node.type === 'text' && typeof node.text === 'string') {
    parts.push(node.text)
  }

  if (Array.isArray(node.content)) {
    for (const child of node.content) walk(child, parts)
  }

  if (
    node.type === 'paragraph' ||
    node.type === 'heading' ||
    node.type === 'listItem' ||
    node.type === 'taskItem' ||
    node.type === 'hardBreak'
  ) {
    parts.push('\n')
  }
}
