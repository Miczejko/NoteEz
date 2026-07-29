<script setup>
defineProps({
  name: { type: String, required: true },
  size: { type: Number, default: 16 },
})
</script>

<template>
  <svg
    class="toolbar-icon"
    viewBox="0 0 20 20"
    :width="size"
    :height="size"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <template v-if="name === 'bold'">
      <path d="M6 4h5a3 3 0 0 1 0 6H6zM6 10h5.5a3 3 0 0 1 0 6H6z" />
    </template>
    <template v-else-if="name === 'italic'">
      <path d="M9 4h6M5 16h6M12 4 8 16" />
    </template>
    <template v-else-if="name === 'strike'">
      <path d="M4 10h12M7 6c0-1 1.2-2 3-2s3.2 1 3 2.2M7 14c0 1 1.2 2 3 2s3.2-1 3-2.5" />
    </template>
    <template v-else-if="name === 'heading'">
      <path d="M5 4v12M13 4v12M5 10h8" />
    </template>
    <template v-else-if="name === 'bulletList'">
      <circle cx="4.5" cy="5" r="1" fill="currentColor" stroke="none" />
      <circle cx="4.5" cy="10" r="1" fill="currentColor" stroke="none" />
      <circle cx="4.5" cy="15" r="1" fill="currentColor" stroke="none" />
      <path d="M8 5h8M8 10h8M8 15h8" />
    </template>
    <template v-else-if="name === 'orderedList'">
      <path d="M8 5h8M8 10h8M8 15h8" />
      <path d="M4 4.5v3M3.3 4.5h1.4M3.3 7.5h1.4M4 10.2c1 0 1.5.4 1.5 1s-.5.8-1 .9c.6.1 1.1.4 1.1 1s-.6 1-1.5 1c-.5 0-.9-.1-1.2-.3" stroke-width="1.1" />
    </template>
    <template v-else-if="name === 'checkbox'">
      <rect x="4" y="4" width="12" height="12" rx="2.5" />
      <path d="M6.5 10.2 9 12.5l4.5-5.2" />
    </template>
    <template v-else-if="name === 'quote'">
      <path d="M5.5 6.5c-1.4 0-2.5 1.1-2.5 2.5S4.1 11.5 5.5 11.5c.2 1.6-.7 2.9-2 3.5M12.5 6.5c-1.4 0-2.5 1.1-2.5 2.5s1.1 2.5 2.5 2.5c.2 1.6-.7 2.9-2 3.5" />
    </template>
    <template v-else-if="name === 'code'">
      <path d="M7 5 3 10l4 5M13 5l4 5-4 5M11 4 9 16" />
    </template>
    <template v-else-if="name === 'drawing'">
      <path d="M4 16 4.7 13 13 4.7a1.5 1.5 0 0 1 2.1 2.1L7 15z" />
      <path d="M11.2 6.5 13.5 8.8" />
    </template>
    <template v-else-if="name === 'eraser'">
      <rect x="5.2" y="8" width="10" height="6" rx="1.3" transform="rotate(-25 10 11)" />
      <path d="M4.5 15.5h11" />
    </template>
    <template v-else-if="name === 'fullscreen'">
      <path d="M4 8V4h4M16 8V4h-4M4 12v4h4M16 12v4h-4" />
    </template>
    <template v-else-if="name === 'mic'">
      <rect x="8" y="3" width="4" height="8" rx="2" />
      <path d="M5 9a5 5 0 0 0 10 0M10 14v3M7.5 17h5" />
    </template>
    <template v-else-if="name === 'device'">
      <rect x="4" y="3" width="12" height="14" rx="1.5" />
      <path d="M8.3 15h3.4" />
    </template>
    <template v-else-if="name === 'trash'">
      <path d="M4 6h12M8 6V4.5A1.5 1.5 0 0 1 9.5 3h1A1.5 1.5 0 0 1 12 4.5V6M6 6l.7 9.5A1.5 1.5 0 0 0 8.2 17h3.6a1.5 1.5 0 0 0 1.5-1.5L14 6" />
    </template>
    <template v-else-if="name === 'clipboard'">
      <rect x="5" y="4" width="10" height="13" rx="1.5" />
      <path d="M8 4V3.5A1.5 1.5 0 0 1 9.5 2h1A1.5 1.5 0 0 1 12 3.5V4" />
      <path d="M7.5 9h5M7.5 12h5" />
    </template>
    <template v-else-if="name === 'gear'">
      <g>
        <rect
          v-for="angle in [0, 45, 90, 135, 180, 225, 270, 315]"
          :key="angle"
          x="8.85"
          y="2.4"
          width="2.3"
          height="2.6"
          rx="0.5"
          fill="currentColor"
          stroke="none"
          :transform="`rotate(${angle} 10 10)`"
        />
        <circle cx="10" cy="10" r="6.1" />
        <circle cx="10" cy="10" r="2.3" />
      </g>
    </template>
    <template v-else-if="name === 'arrow-left'">
      <path d="M12.5 4.5 6 10l6.5 5.5" />
    </template>
    <template v-else-if="name === 'calendar'">
      <rect x="3.5" y="4.5" width="13" height="12" rx="1.8" />
      <path d="M3.5 8.3h13M7 3v2.6M13 3v2.6" />
    </template>
    <template v-else-if="name === 'chevron-left'">
      <path d="M12 5 7 10l5 5" />
    </template>
    <template v-else-if="name === 'chevron-right'">
      <path d="M8 5l5 5-5 5" />
    </template>
    <template v-else-if="name === 'grid'">
      <rect x="3.5" y="3.5" width="5.5" height="5.5" rx="1" />
      <rect x="11" y="3.5" width="5.5" height="5.5" rx="1" />
      <rect x="3.5" y="11" width="5.5" height="5.5" rx="1" />
      <rect x="11" y="11" width="5.5" height="5.5" rx="1" />
    </template>
    <template v-else-if="name === 'list'">
      <path d="M4 5.5h12M4 10h12M4 14.5h12" />
    </template>
    <template v-else-if="name === 'plus'">
      <path d="M10 4v12M4 10h12" />
    </template>
    <template v-else-if="name === 'close'">
      <path d="M5 5l10 10M15 5 5 15" />
    </template>
  </svg>
</template>

<style scoped>
.toolbar-icon {
  display: block;
}
</style>
