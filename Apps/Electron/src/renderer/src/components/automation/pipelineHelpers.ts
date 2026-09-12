import type { TFunction } from 'i18next'
import type { AutomationPipeline, AutomationStepType } from '../../api/automation'
import type { AutomationTrigger } from './triggers'

export interface AutomationPipelineSections {
  automatic: AutomationPipeline[]
  manual: AutomationPipeline[]
}

export type IdentifiedAutomationPipeline = AutomationPipeline & { id: string }

export type TriggerNameKeyResolver = (trigger: AutomationTrigger) => string | null

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function shortAutomationTypeName(type: string): string {
  return type
    .replace(/AutomationStep$/i, '')
    .replace(/AutomationPipelineTrigger$/i, '')
}

export function splitAutomationPipelines(
  pipelines: AutomationPipeline[]
): AutomationPipelineSections {
  const automatic: AutomationPipeline[] = []
  const manual: AutomationPipeline[] = []

  for (const pipeline of pipelines) {
    if (pipeline.trigger == null) manual.push(pipeline)
    else automatic.push(pipeline)
  }

  return { automatic, manual }
}

export function formatAutomationPipelineTitle(
  pipeline: AutomationPipeline,
  t: TFunction,
  resolveTriggerNameKey: TriggerNameKeyResolver,
  localizeStoredName: (name: string) => string = (name) => name
): string {
  const storedName = pipeline.name?.trim() ?? ''
  if (storedName !== '') return localizeStoredName(storedName)

  if (pipeline.trigger != null) {
    const trigger = pipeline.trigger as AutomationTrigger
    const type = typeof trigger.$type === 'string' ? trigger.$type : ''
    const fallback = shortAutomationTypeName(type)
    const key = resolveTriggerNameKey(trigger)
    return key == null ? fallback : t(key, { defaultValue: fallback })
  }

  return t('wpf.automationPipelineControlunnamed', {
    defaultValue: t('automation.quickAction')
  })
}

export function formatAutomationPipelineSubtitle(
  pipeline: AutomationPipeline,
  t: TFunction
): string {
  const count = pipeline.steps?.length ?? 0
  const key =
    count === 1 ? 'wpf.automationPipelineControlstep' : 'wpf.automationPipelineControlstepmany'
  return t(key, { defaultValue: `${t('automation.steps')} (${count})` }).replace(
    '{0}',
    String(count)
  )
}

export function formatAutomationStepTitle(
  step: AutomationStepType,
  t: TFunction
): string {
  const type = String(step.$type)
  return t(`automation.stepEditors.${type}.title`, {
    defaultValue: shortAutomationTypeName(type)
  })
}

export interface AutomationDraftCommit<T> {
  value: T
  dirty: boolean
}

/** Keep the in-memory draft unless the remote operation explicitly succeeded. */
export function commitAutomationDraft<T>(
  succeeded: boolean,
  draft: T,
  canonical: T
): AutomationDraftCommit<T> {
  if (succeeded !== true) {
    return { value: draft, dirty: true }
  }
  return { value: canonical, dirty: false }
}

export function isValidAutomationPipelineId(value: unknown): value is string {
  return typeof value === 'string' && GUID_PATTERN.test(value)
}

export function createAutomationPipeline(
  pipelines: AutomationPipeline[],
  name: string,
  trigger: AutomationPipeline['trigger'],
  createId: () => string
): IdentifiedAutomationPipeline | null {
  const trimmedName = name.trim()
  if (trimmedName === '') return null

  const id = createId().trim()
  if (!isValidAutomationPipelineId(id)) return null
  if (pipelines.some((pipeline) => pipeline.id?.toLowerCase() === id.toLowerCase())) return null

  return {
    id,
    name: trimmedName,
    trigger: trigger ?? null,
    steps: [],
    isExclusive: trigger != null
  }
}

function findUniquePipelineIndex(pipelines: AutomationPipeline[], pipelineId: string): number {
  if (pipelineId.trim() === '') return -1

  let found = -1
  for (let index = 0; index < pipelines.length; index += 1) {
    if (pipelines[index].id !== pipelineId) continue
    if (found !== -1) return -1
    found = index
  }
  return found
}

function updatePipeline(
  pipelines: AutomationPipeline[],
  pipelineId: string,
  update: (pipeline: AutomationPipeline) => AutomationPipeline
): AutomationPipeline[] {
  const index = findUniquePipelineIndex(pipelines, pipelineId)
  if (index === -1) return pipelines

  const nextPipeline = update(pipelines[index])
  if (nextPipeline === pipelines[index]) return pipelines

  const next = [...pipelines]
  next[index] = nextPipeline
  return next
}

export function appendAutomationStep(
  pipelines: AutomationPipeline[],
  pipelineId: string,
  step: AutomationStepType
): AutomationPipeline[] {
  if (step.$type.trim() === '') return pipelines
  return updatePipeline(pipelines, pipelineId, (pipeline) => ({
    ...pipeline,
    steps: [...(pipeline.steps ?? []), step]
  }))
}

export function removeAutomationStep(
  pipelines: AutomationPipeline[],
  pipelineId: string,
  index: number
): AutomationPipeline[] {
  if (!Number.isInteger(index)) return pipelines
  return updatePipeline(pipelines, pipelineId, (pipeline) => {
    const steps = pipeline.steps ?? []
    if (index < 0 || index >= steps.length) return pipeline
    return {
      ...pipeline,
      steps: steps.filter((_, stepIndex) => stepIndex !== index)
    }
  })
}

export function moveAutomationStep(
  pipelines: AutomationPipeline[],
  pipelineId: string,
  from: number,
  to: number
): AutomationPipeline[] {
  if (!Number.isInteger(from) || !Number.isInteger(to) || from === to) return pipelines
  return updatePipeline(pipelines, pipelineId, (pipeline) => {
    const steps = pipeline.steps ?? []
    if (from < 0 || from >= steps.length || to < 0 || to >= steps.length) return pipeline

    const nextSteps = [...steps]
    const [moved] = nextSteps.splice(from, 1)
    nextSteps.splice(to, 0, moved)
    return { ...pipeline, steps: nextSteps }
  })
}

export function moveAutomationPipeline(
  pipelines: AutomationPipeline[],
  pipelineId: string,
  offset: number
): AutomationPipeline[] {
  if (!Number.isInteger(offset) || offset === 0) return pipelines

  const fromIndex = findUniquePipelineIndex(pipelines, pipelineId)
  if (fromIndex === -1) return pipelines

  const isManual = pipelines[fromIndex].trigger == null
  const sectionIndexes = pipelines
    .map((pipeline, index) => ({ index, isManual: pipeline.trigger == null }))
    .filter((entry) => entry.isManual === isManual)
    .map((entry) => entry.index)
  const sectionIndex = sectionIndexes.indexOf(fromIndex)
  const targetSectionIndex = sectionIndex + offset
  if (targetSectionIndex < 0 || targetSectionIndex >= sectionIndexes.length) return pipelines

  const targetIndex = sectionIndexes[targetSectionIndex]
  const next = [...pipelines]
  const [moved] = next.splice(fromIndex, 1)
  next.splice(targetIndex, 0, moved)
  return next
}
