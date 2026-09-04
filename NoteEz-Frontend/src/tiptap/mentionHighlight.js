import { Extension } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'

// Czysto wizualne podswietlenie "@nazwaUzytkownika" w tekscie - nie zmienia
// dokumentu ani zapisanego JSON-a (backend i tak parsuje surowy tekst regexem
// przy zapisie), wiec jest bezpieczne wzgledem istniejacych notatek.
const MENTION_PATTERN = /@[A-Za-z0-9_.-]{3,32}/g

function buildDecorations(doc) {
  const decorations = []
  doc.descendants((node, pos) => {
    if (!node.isText) return
    for (const match of node.text.matchAll(MENTION_PATTERN)) {
      const from = pos + match.index
      const to = from + match[0].length
      decorations.push(Decoration.inline(from, to, { class: 'mention-chip' }))
    }
  })
  return DecorationSet.create(doc, decorations)
}

export const MentionHighlight = Extension.create({
  name: 'mentionHighlight',

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('mentionHighlight'),
        state: {
          init: (_, { doc }) => buildDecorations(doc),
          apply: (tr, old) => (tr.docChanged ? buildDecorations(tr.doc) : old),
        },
        props: {
          decorations(state) {
            return this.getState(state)
          },
        },
      }),
    ]
  },
})
