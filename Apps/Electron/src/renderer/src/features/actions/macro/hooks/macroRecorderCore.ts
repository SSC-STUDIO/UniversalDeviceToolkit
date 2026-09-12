import type { MacroEvent, MacroRecordingMode } from '../api/macroClient'

export type MacroRecorderState = 'idle' | 'preparing' | 'recording'

type RecorderEventListener = (event: Event) => void

export interface MacroRecorderEventTarget {
  addEventListener(
    type: string,
    listener: RecorderEventListener,
    options?: boolean | AddEventListenerOptions
  ): void
  removeEventListener(
    type: string,
    listener: RecorderEventListener,
    options?: boolean | EventListenerOptions
  ): void
}

export interface MacroRecorderEnvironment {
  target: MacroRecorderEventTarget
  now: () => number
  setTimer: (callback: () => void, delayMs: number) => number
  clearTimer: (timer: number) => void
}

export interface MacroRecorderCallbacks {
  onStateChange: (state: MacroRecorderState) => void
  onComplete: (events: MacroEvent[], interrupted: boolean) => void
}

const VK_ESCAPE = 0x1b
const PREPARING_DELAY_MS = 3000

const MOUSE_BUTTON_KEYS: Readonly<Record<number, number>> = {
  0: 1,
  1: 3,
  2: 2,
  3: 1 << 16,
  4: 2 << 16
}

export class MacroRecorderController {
  private recorderState: MacroRecorderState = 'idle'
  private mode: MacroRecordingMode = 'Keyboard'
  private events: MacroEvent[] = []
  private lastEventTime = 0
  private preparingTimer: number | null = null
  private listenersAttached = false
  private readonly environment: MacroRecorderEnvironment
  private readonly callbacks: MacroRecorderCallbacks

  public constructor(
    environment: MacroRecorderEnvironment,
    callbacks: MacroRecorderCallbacks
  ) {
    this.environment = environment
    this.callbacks = callbacks
  }

  public get state(): MacroRecorderState {
    return this.recorderState
  }

  public start(mode: MacroRecordingMode): void {
    this.releaseResources()
    this.events = []
    this.lastEventTime = 0
    this.mode = mode

    if (mode === 'KeyboardMouseMovement') {
      this.transitionTo('preparing')
      this.preparingTimer = this.environment.setTimer(() => {
        this.preparingTimer = null
        this.beginRecording()
      }, PREPARING_DELAY_MS)
      return
    }

    this.beginRecording()
  }

  public stop(): void {
    if (this.recorderState === 'idle') return
    this.finish(false)
  }

  public cancel(): void {
    if (this.recorderState === 'idle') return
    this.finish(true)
  }

  public dispose(): void {
    this.releaseResources()
    this.events = []
    this.lastEventTime = 0
    this.recorderState = 'idle'
  }

  private readonly handleKeyDown: RecorderEventListener = (rawEvent) => {
    const event = rawEvent as KeyboardEvent
    if (event.keyCode === VK_ESCAPE) {
      event.preventDefault()
      this.finish(true)
      return
    }
    if (event.repeat) return
    this.append({
      source: 'Keyboard',
      direction: 'Down',
      key: event.keyCode,
      x: 0,
      y: 0
    })
  }

  private readonly handleKeyUp: RecorderEventListener = (rawEvent) => {
    const event = rawEvent as KeyboardEvent
    if (event.keyCode === VK_ESCAPE) return
    this.append({
      source: 'Keyboard',
      direction: 'Up',
      key: event.keyCode,
      x: 0,
      y: 0
    })
  }

  private readonly handleMouseDown: RecorderEventListener = (rawEvent) => {
    if (this.mode === 'Keyboard') return
    const event = rawEvent as MouseEvent
    const key = MOUSE_BUTTON_KEYS[event.button]
    if (key === undefined) return
    this.append({
      source: 'Mouse',
      direction: 'Down',
      key,
      x: event.clientX,
      y: event.clientY
    })
  }

  private readonly handleMouseUp: RecorderEventListener = (rawEvent) => {
    if (this.mode === 'Keyboard') return
    const event = rawEvent as MouseEvent
    const key = MOUSE_BUTTON_KEYS[event.button]
    if (key === undefined) return
    this.append({
      source: 'Mouse',
      direction: 'Up',
      key,
      x: event.clientX,
      y: event.clientY
    })
  }

  private readonly handleWheel: RecorderEventListener = (rawEvent) => {
    if (this.mode === 'Keyboard') return
    const event = rawEvent as WheelEvent
    if (event.deltaY !== 0) {
      this.append({
        source: 'Mouse',
        direction: 'Wheel',
        key: Math.round(-event.deltaY),
        x: event.clientX,
        y: event.clientY
      })
    }
    if (event.deltaX !== 0) {
      this.append({
        source: 'Mouse',
        direction: 'HorizontalWheel',
        key: Math.round(event.deltaX),
        x: event.clientX,
        y: event.clientY
      })
    }
  }

  private readonly handleMouseMove: RecorderEventListener = (rawEvent) => {
    if (this.mode !== 'KeyboardMouseMovement') return
    const event = rawEvent as MouseEvent
    this.append({
      source: 'Mouse',
      direction: 'Move',
      key: 0,
      x: event.clientX,
      y: event.clientY
    })
  }

  private beginRecording(): void {
    this.transitionTo('recording')
    this.attachListeners()
  }

  private append(event: Omit<MacroEvent, 'delayMs'>): void {
    if (this.recorderState !== 'recording') return
    const now = this.environment.now()
    const delayMs = this.events.length === 0 ? 0 : Math.max(0, now - this.lastEventTime)
    this.lastEventTime = now
    this.events.push({ ...event, delayMs })
  }

  private finish(interrupted: boolean): void {
    const captured = this.events.map((event) => ({ ...event }))
    this.releaseResources()
    this.events = []
    this.lastEventTime = 0
    this.transitionTo('idle')
    this.callbacks.onComplete(captured, interrupted)
  }

  private transitionTo(state: MacroRecorderState): void {
    if (this.recorderState === state) return
    this.recorderState = state
    this.callbacks.onStateChange(state)
  }

  private attachListeners(): void {
    if (this.listenersAttached) return
    const target = this.environment.target
    target.addEventListener('keydown', this.handleKeyDown, true)
    target.addEventListener('keyup', this.handleKeyUp, true)
    target.addEventListener('mousedown', this.handleMouseDown, true)
    target.addEventListener('mouseup', this.handleMouseUp, true)
    target.addEventListener('wheel', this.handleWheel, true)
    target.addEventListener('mousemove', this.handleMouseMove, true)
    this.listenersAttached = true
  }

  private releaseResources(): void {
    if (this.preparingTimer !== null) {
      this.environment.clearTimer(this.preparingTimer)
      this.preparingTimer = null
    }

    if (!this.listenersAttached) return
    const target = this.environment.target
    target.removeEventListener('keydown', this.handleKeyDown, true)
    target.removeEventListener('keyup', this.handleKeyUp, true)
    target.removeEventListener('mousedown', this.handleMouseDown, true)
    target.removeEventListener('mouseup', this.handleMouseUp, true)
    target.removeEventListener('wheel', this.handleWheel, true)
    target.removeEventListener('mousemove', this.handleMouseMove, true)
    this.listenersAttached = false
  }
}
