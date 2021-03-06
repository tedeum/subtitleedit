﻿using Nikse.SubtitleEdit.Core;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class SettingsFixContinuationStyle : Form
    {
        public SettingsFixContinuationStyle()
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            UiUtil.FixFonts(this);

            var language = Configuration.Settings.Language.Settings;
            var settings = Configuration.Settings.General;
            Text = language.FixContinuationStyleSettings;
            checkBoxUncheckInsertsAllCaps.Text = language.UncheckInsertsAllCaps;
            checkBoxUncheckInsertsItalic.Text = language.UncheckInsertsItalic;
            checkBoxUncheckInsertsLowercase.Text = language.UncheckInsertsLowercase;
            checkBoxHideContinuationCandidatesWithoutName.Text = language.HideContinuationCandidatesWithoutName;
            checkBoxIgnoreLyrics.Text = language.IgnoreLyrics;

            checkBoxUncheckInsertsAllCaps.Checked = settings.FixContinuationStyleUncheckInsertsAllCaps;
            checkBoxUncheckInsertsItalic.Checked = settings.FixContinuationStyleUncheckInsertsItalic;
            checkBoxUncheckInsertsLowercase.Checked = settings.FixContinuationStyleUncheckInsertsLowercase;
            checkBoxHideContinuationCandidatesWithoutName.Checked = settings.FixContinuationStyleHideContinuationCandidatesWithoutName;
            checkBoxIgnoreLyrics.Checked = settings.FixContinuationStyleIgnoreLyrics;

            buttonOK.Text = Configuration.Settings.Language.General.Ok;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            UiUtil.FixLargeFonts(this, buttonOK);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            Configuration.Settings.General.FixContinuationStyleUncheckInsertsAllCaps = checkBoxUncheckInsertsAllCaps.Checked;
            Configuration.Settings.General.FixContinuationStyleUncheckInsertsItalic = checkBoxUncheckInsertsItalic.Checked;
            Configuration.Settings.General.FixContinuationStyleUncheckInsertsLowercase = checkBoxUncheckInsertsLowercase.Checked;
            Configuration.Settings.General.FixContinuationStyleHideContinuationCandidatesWithoutName = checkBoxHideContinuationCandidatesWithoutName.Checked;
            Configuration.Settings.General.FixContinuationStyleIgnoreLyrics = checkBoxIgnoreLyrics.Checked;

            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void SettingsFixContinuationStyle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
