package com.bl0cked

import android.app.AlertDialog
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.EditText
import android.widget.TextView
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.bl0cked.models.Chore
import com.google.android.material.button.MaterialButton
import kotlinx.coroutines.launch

/**
 * Fragment for managing chores
 */
class ChoresFragment : Fragment() {

    private lateinit var connectionManager: ConnectionManager
    private lateinit var choresRecyclerView: RecyclerView
    private lateinit var addChoreButton: MaterialButton
    private var chores = mutableListOf<Chore>()
    private lateinit var choreAdapter: ChoreAdapter

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        return inflater.inflate(R.layout.fragment_chores, container, false)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        connectionManager = ConnectionManager(requireContext())

        setupViews(view)
        loadChores()
    }

    private fun setupViews(view: View) {
        choresRecyclerView = view.findViewById(R.id.choresRecyclerView)
        addChoreButton = view.findViewById(R.id.addChoreButton)

        choreAdapter = ChoreAdapter(
            chores = chores,
            onEditClick = { chore -> showEditChoreDialog(chore) },
            onDeleteClick = { chore -> deleteChore(chore) }
        )

        choresRecyclerView.layoutManager = LinearLayoutManager(requireContext())
        choresRecyclerView.adapter = choreAdapter

        addChoreButton.setOnClickListener {
            showAddChoreDialog()
        }
    }

    private fun loadChores() {
        lifecycleScope.launch {
            val result = connectionManager.getChores()

            if (result.isSuccess) {
                chores.clear()
                chores.addAll(result.getOrNull() ?: emptyList())
                choreAdapter.notifyDataSetChanged()
            } else {
                Toast.makeText(requireContext(), "Failed to load chores", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun showAddChoreDialog() {
        val dialogView = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_add_chore, null)
        val choreInput = dialogView.findViewById<EditText>(R.id.choreInput)

        AlertDialog.Builder(requireContext())
            .setTitle("Add New Chore")
            .setView(dialogView)
            .setPositiveButton("Add") { _, _ ->
                val choreText = choreInput.text.toString()
                if (choreText.isNotEmpty()) {
                    addChore(choreText)
                }
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun showEditChoreDialog(chore: Chore) {
        val dialogView = LayoutInflater.from(requireContext()).inflate(R.layout.dialog_add_chore, null)
        val choreInput = dialogView.findViewById<EditText>(R.id.choreInput)
        choreInput.setText(chore.text)

        AlertDialog.Builder(requireContext())
            .setTitle("Edit Chore")
            .setView(dialogView)
            .setPositiveButton("Save") { _, _ ->
                val choreText = choreInput.text.toString()
                if (choreText.isNotEmpty()) {
                    chore.text = choreText
                    updateChores()
                }
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun addChore(choreText: String) {
        val newId = (chores.maxOfOrNull { it.id } ?: 0) + 1
        val newChore = Chore(id = newId, text = choreText, checked = false)
        chores.add(newChore)
        updateChores()
    }

    private fun deleteChore(chore: Chore) {
        AlertDialog.Builder(requireContext())
            .setTitle("Delete Chore")
            .setMessage("Are you sure you want to delete this chore?")
            .setPositiveButton("Delete") { _, _ ->
                chores.remove(chore)
                updateChores()
            }
            .setNegativeButton("Cancel", null)
            .show()
    }

    private fun updateChores() {
        lifecycleScope.launch {
            val result = connectionManager.updateChores(chores)

            if (result.isSuccess) {
                choreAdapter.notifyDataSetChanged()
                Toast.makeText(requireContext(), "Chores updated", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(requireContext(), "Failed to update chores", Toast.LENGTH_SHORT).show()
            }
        }
    }
}

/**
 * RecyclerView adapter for chores
 */
class ChoreAdapter(
    private val chores: MutableList<Chore>,
    private val onEditClick: (Chore) -> Unit,
    private val onDeleteClick: (Chore) -> Unit
) : RecyclerView.Adapter<ChoreAdapter.ChoreViewHolder>() {

    class ChoreViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val choreNumber: TextView = view.findViewById(R.id.choreNumber)
        val choreText: TextView = view.findViewById(R.id.choreText)
        val editButton: MaterialButton = view.findViewById(R.id.editButton)
        val deleteButton: MaterialButton = view.findViewById(R.id.deleteButton)
        val checkedIndicator: TextView = view.findViewById(R.id.checkedIndicator)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ChoreViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_chore, parent, false)
        return ChoreViewHolder(view)
    }

    override fun onBindViewHolder(holder: ChoreViewHolder, position: Int) {
        val chore = chores[position]

        holder.choreNumber.text = chore.id.toString()
        holder.choreText.text = chore.text
        holder.checkedIndicator.visibility = if (chore.checked) View.VISIBLE else View.GONE

        holder.editButton.setOnClickListener { onEditClick(chore) }
        holder.deleteButton.setOnClickListener { onDeleteClick(chore) }
    }

    override fun getItemCount() = chores.size
}
