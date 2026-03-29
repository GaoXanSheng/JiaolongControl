<script setup lang="ts">
defineProps({
	title: {
		type: String,
		required: true
	}
})
</script>

<template>
	<a-col :span="16">
		<a-card hoverable>
			<div
				:style="{
					display: 'flex',
					alignItems: 'center',
					justifyContent: 'space-between'
				}"
			>
				<span>
					<a-typography-text>{{ title }}</a-typography-text>
				</span>
				<slot name="extra"></slot>
			</div>
		</a-card>
	</a-col>
</template>

<style scoped></style>
