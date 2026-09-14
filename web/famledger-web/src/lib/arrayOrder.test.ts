import { describe, expect, it } from 'vitest'
import { moveItemInArray } from './arrayOrder'

describe('moveItemInArray', () => {
  it('moves item forward', () => {
    expect(moveItemInArray(['a', 'b', 'c', 'd'], 0, 2)).toEqual(['b', 'c', 'a', 'd'])
  })

  it('moves item backward', () => {
    expect(moveItemInArray(['a', 'b', 'c', 'd'], 3, 1)).toEqual(['a', 'd', 'b', 'c'])
  })

  it('returns a copy when index unchanged', () => {
    const input = ['a', 'b', 'c']
    const result = moveItemInArray(input, 1, 1)
    expect(result).toEqual(input)
    expect(result).not.toBe(input)
  })

  it('ignores out of range indexes', () => {
    expect(moveItemInArray(['a', 'b'], -1, 1)).toEqual(['a', 'b'])
    expect(moveItemInArray(['a', 'b'], 0, 5)).toEqual(['a', 'b'])
  })
})
