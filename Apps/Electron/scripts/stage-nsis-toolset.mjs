import { cp, mkdir, rm } from 'node:fs/promises'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'

const destination = process.argv[2]
if (!destination) throw new Error('NSIS toolset destination is required.')

const require = createRequire(import.meta.url)
const { getNsisElevatePath } = require('app-builder-lib/out/toolsets/windows')
const elevatePath = await getNsisElevatePath(undefined, undefined)
const sourceDirectory = dirname(elevatePath)
const destinationDirectory = resolve(destination)

await rm(destinationDirectory, { recursive: true, force: true })
await mkdir(dirname(destinationDirectory), { recursive: true })
await cp(sourceDirectory, destinationDirectory, { recursive: true })
console.log(`NSIS toolset staged at ${destinationDirectory}`)
