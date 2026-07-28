import { Node, mergeAttributes } from '@tiptap/core'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import DrawingNodeView from '../components/DrawingNodeView.vue'

// Embeds a note drawing inline in the text flow instead of a separate tab.
export const DrawingBlock = Node.create({
  name: 'drawingBlock',
  group: 'block',
  atom: true,
  draggable: true,
  isolating: true,

  addOptions() {
    return { noteId: null }
  },

  addAttributes() {
    return {
      drawingId: { default: null },
    }
  },

  parseHTML() {
    return [{ tag: 'div[data-type="drawing-block"]' }]
  },

  renderHTML({ HTMLAttributes }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-type': 'drawing-block' })]
  },

  addNodeView() {
    return VueNodeViewRenderer(DrawingNodeView)
  },
})
