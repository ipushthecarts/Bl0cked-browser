package com.bl0cked.models

/**
 * Represents a single chore item
 */
data class Chore(
    val id: Int = 0,
    var text: String = "",
    var checked: Boolean = false
)

/**
 * Container for the chore list
 */
data class ChoreData(
    val chores: MutableList<Chore> = mutableListOf()
)
